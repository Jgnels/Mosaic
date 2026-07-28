using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;

namespace Dagmay.RimWorld.Perception
{
    public sealed class ReadOnlyRimWorldEventProjection
    {
        internal ReadOnlyRimWorldEventProjection(RimWorldEventEvidenceEnvelope envelope)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            WitnessIds = new ReadOnlyCollection<IndividualId>(Array.Empty<IndividualId>());
        }

        public RimWorldEventEvidenceEnvelope Envelope { get; }
        public IReadOnlyList<IndividualId> WitnessIds { get; }
    }

    /// <summary>
    /// Pure projection from already-admitted social facts into the inert Mosaic 0.3
    /// RimWorld evidence contract. It owns no game object, durable state, or authority.
    /// </summary>
    public static class ReadOnlySocialEventEnvelopeAdapter
    {
        public const string OpinionChanged = "rimworld.social.opinion_changed";
        public const string DirectRelationshipChanged = "rimworld.relationship.direct_changed";
        public const string SourceAdapter = "Mosaic.RimWorld.ReadOnlySocialEventAdapter.v1";
        public const string ProjectionSchema = "mosaic.rimworld.read-only-social-event-projection.v1";

        public static ReadOnlyRimWorldEventProjection? TryProject(
            EventId sourceEventId,
            long admittedGameTick,
            string eventKind,
            IDictionary<string, string> factualPayload,
            IndividualId? actorId,
            IndividualId? targetId)
        {
            if (!IsSupported(eventKind)) return null;
            if (sourceEventId.Value == Guid.Empty ||
                admittedGameTick < 0 ||
                !actorId.HasValue ||
                actorId.Value.Value == Guid.Empty ||
                !targetId.HasValue ||
                targetId.Value.Value == Guid.Empty ||
                actorId.Value == targetId.Value)
            {
                return null;
            }

            var normalizedPayload = NormalizePayload(factualPayload);
            if (normalizedPayload is null || normalizedPayload.Count == 0) return null;

            var deduplicationKey = ComputeDeduplicationKey(
                sourceEventId,
                admittedGameTick,
                eventKind,
                actorId.Value,
                targetId.Value,
                normalizedPayload);
            var envelope = new RimWorldEventEvidenceEnvelope(
                sourceEventId,
                eventKind,
                admittedGameTick,
                actorId.Value,
                targetId.Value,
                new[] { actorId.Value, targetId.Value },
                RimWorldEvidenceOutcomeState.Observed,
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                SourceAdapter,
                deduplicationKey);
            return new ReadOnlyRimWorldEventProjection(envelope);
        }

        private static bool IsSupported(string eventKind) =>
            string.Equals(eventKind, OpinionChanged, StringComparison.Ordinal) ||
            string.Equals(eventKind, DirectRelationshipChanged, StringComparison.Ordinal);

        private static SortedDictionary<string, string>? NormalizePayload(
            IDictionary<string, string> factualPayload)
        {
            if (factualPayload is null || factualPayload.Count == 0 || factualPayload.Count > 32)
                return null;

            var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in factualPayload)
            {
                var key = NormalizeText(pair.Key, 128);
                var value = NormalizeText(pair.Value, 1024);
                if (key is null || value is null) return null;
                if (IsDisplayLabelKey(key)) continue;
                if (normalized.ContainsKey(key)) return null;
                normalized.Add(key, value);
            }

            return normalized;
        }

        private static string ComputeDeduplicationKey(
            EventId sourceEventId,
            long admittedGameTick,
            string eventKind,
            IndividualId actorId,
            IndividualId targetId,
            IEnumerable<KeyValuePair<string, string>> normalizedPayload)
        {
            var canonical = new StringBuilder();
            Append(canonical, "schema", ProjectionSchema);
            Append(canonical, "event_id", sourceEventId.ToString());
            Append(canonical, "event_kind", eventKind);
            Append(canonical, "game_tick", admittedGameTick.ToString(CultureInfo.InvariantCulture));
            Append(canonical, "actor_id", actorId.ToString());
            Append(canonical, "target_id", targetId.ToString());
            foreach (var pair in normalizedPayload)
            {
                Append(canonical, "payload_key", pair.Key);
                Append(canonical, "payload_value", pair.Value);
            }

            using (var algorithm = SHA256.Create())
            {
                var digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return string.Concat(
                    digest.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void Append(StringBuilder builder, string field, string value)
        {
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
            builder.Append('=');
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }

        private static string? NormalizeText(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();
            return normalized.Length == 0 || normalized.Length > maximumLength
                ? null
                : normalized;
        }

        private static bool IsDisplayLabelKey(string key) =>
            string.Equals(key, "target_name", StringComparison.Ordinal) ||
            string.Equals(key, "display_name_at_event", StringComparison.Ordinal) ||
            string.Equals(key, "display_name", StringComparison.Ordinal) ||
            string.Equals(key, "display_label", StringComparison.Ordinal) ||
            string.Equals(key, "actor_name", StringComparison.Ordinal) ||
            string.Equals(key, "pawn_name", StringComparison.Ordinal) ||
            key.EndsWith("_display_name", StringComparison.Ordinal) ||
            key.EndsWith("_display_label", StringComparison.Ordinal);
    }

    /// <summary>
    /// Session-local admission and duplicate gate used by the component notification seam.
    /// It stores only source EventIds and never writes canonical or durable state.
    /// </summary>
    public sealed class ReadOnlySocialEventEnvelopeCapture
    {
        private readonly object _gate = new object();
        private readonly HashSet<EventId> _notifiedEventIds = new HashSet<EventId>();

        public ReadOnlyRimWorldEventProjection? TryCapture(
            bool experienceJournalAdmitted,
            bool eventLedgerAdmitted,
            EventId sourceEventId,
            long admittedGameTick,
            string eventKind,
            IDictionary<string, string> factualPayload,
            IndividualId? actorId,
            IndividualId? targetId)
        {
            if (!experienceJournalAdmitted || !eventLedgerAdmitted) return null;
            var projection = ReadOnlySocialEventEnvelopeAdapter.TryProject(
                sourceEventId,
                admittedGameTick,
                eventKind,
                factualPayload,
                actorId,
                targetId);
            if (projection is null) return null;

            lock (_gate)
            {
                return _notifiedEventIds.Add(sourceEventId)
                    ? projection
                    : null;
            }
        }
    }
}
