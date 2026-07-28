using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;

namespace Dagmay.Core.Appraisal
{
    public enum GroundedSocialChangeKind
    {
        OpinionChanged = 0,
        DirectRelationshipChanged = 1
    }

    public static class GroundedSocialProjectionHash
    {
        public const string OpinionChanged = "rimworld.social.opinion_changed";
        public const string DirectRelationshipChanged = "rimworld.relationship.direct_changed";
        public const string SourceAdapter = "Mosaic.RimWorld.ReadOnlySocialEventAdapter.v1";
        public const string ProjectionSchema = "mosaic.rimworld.read-only-social-event-projection.v1";

        public static string ComputePr10DeduplicationKey(
            EventId eventId,
            string eventKind,
            long tick,
            IndividualId actorId,
            IndividualId targetId,
            IDictionary<string, string> factualPayload)
        {
            if (eventId.Value == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (actorId.Value == Guid.Empty || targetId.Value == Guid.Empty || actorId == targetId)
                throw new ArgumentException("A social projection requires distinct actor and target IDs.");
            var payload = NormalizePayload(factualPayload);
            var canonical = new StringBuilder();
            Append(canonical, "schema", ProjectionSchema);
            Append(canonical, "event_id", eventId.ToString());
            Append(canonical, "event_kind", RequireText(eventKind, nameof(eventKind), 256));
            Append(canonical, "game_tick", tick.ToString(CultureInfo.InvariantCulture));
            Append(canonical, "actor_id", actorId.ToString());
            Append(canonical, "target_id", targetId.ToString());
            foreach (var pair in payload)
            {
                if (IsDisplayLabelKey(pair.Key)) continue;
                Append(canonical, "payload_key", pair.Key);
                Append(canonical, "payload_value", pair.Value);
            }
            return Sha256(canonical.ToString());
        }

        internal static IReadOnlyDictionary<string, string> NormalizePayload(
            IDictionary<string, string> factualPayload)
        {
            if (factualPayload is null) throw new ArgumentNullException(nameof(factualPayload));
            if (factualPayload.Count < 1 || factualPayload.Count > 32)
                throw new ArgumentOutOfRangeException(nameof(factualPayload));
            var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in factualPayload)
            {
                var key = RequireText(pair.Key, nameof(factualPayload), 128);
                var value = NormalizeValue(pair.Value, nameof(factualPayload), 1024);
                if (normalized.ContainsKey(key))
                    throw new ArgumentException("Factual payload keys must be unique.", nameof(factualPayload));
                normalized.Add(key, value);
            }
            return new ReadOnlyDictionary<string, string>(normalized);
        }

        internal static bool IsDisplayLabelKey(string key) =>
            string.Equals(key, "target_name", StringComparison.Ordinal) ||
            string.Equals(key, "display_name_at_event", StringComparison.Ordinal) ||
            string.Equals(key, "display_name", StringComparison.Ordinal) ||
            string.Equals(key, "display_label", StringComparison.Ordinal) ||
            string.Equals(key, "actor_name", StringComparison.Ordinal) ||
            string.Equals(key, "pawn_name", StringComparison.Ordinal) ||
            key.EndsWith("_display_name", StringComparison.Ordinal) ||
            key.EndsWith("_display_label", StringComparison.Ordinal);

        internal static string LowerHex(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                throw new ArgumentException("Value must be lowercase SHA-256.", parameterName);
            foreach (var character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                    throw new ArgumentException("Value must be lowercase SHA-256.", parameterName);
            }
            return value;
        }

        internal static string HashFields(IEnumerable<KeyValuePair<string, string>> fields)
        {
            var builder = new StringBuilder();
            foreach (var field in fields) Append(builder, field.Key, field.Value);
            return Sha256(builder.ToString());
        }

        internal static string RequireText(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("A bounded nonblank value is required.", parameterName);
            return value;
        }

        private static string NormalizeValue(string value, string parameterName, int maximum)
        {
            if (value is null) throw new ArgumentNullException(parameterName);
            var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (normalized.Length > maximum)
                throw new ArgumentException("Factual payload value exceeds its bound.", parameterName);
            return normalized;
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

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes)
                    builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }

    public sealed class GroundedSocialSemanticEvidence
    {
        private static readonly HashSet<string> OpinionRequired =
            new HashSet<string>(new[]
            {
                "target_external_id", "opinion_before", "opinion_after", "opinion_delta"
            }, StringComparer.Ordinal);
        private static readonly HashSet<string> RelationRequired =
            new HashSet<string>(new[]
            {
                "target_external_id", "relations_before", "relations_after"
            }, StringComparer.Ordinal);
        private static readonly HashSet<string> OptionalProvenance =
            new HashSet<string>(new[] { "pawn_external_id" }, StringComparer.Ordinal);

        public GroundedSocialSemanticEvidence(
            RimWorldEventEvidenceEnvelope envelope,
            long sourceAdmissionPosition,
            string observationBatchId,
            IDictionary<string, string> factualPayload)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            if (sourceAdmissionPosition < 1) throw new ArgumentOutOfRangeException(nameof(sourceAdmissionPosition));
            ObservationBatchId = GroundedSocialProjectionHash.LowerHex(
                observationBatchId,
                nameof(observationBatchId));
            if (!Envelope.ActorId.HasValue || !Envelope.TargetId.HasValue ||
                Envelope.ActorId.Value.Value == Guid.Empty || Envelope.TargetId.Value.Value == Guid.Empty ||
                Envelope.ActorId.Value == Envelope.TargetId.Value)
                throw new ArgumentException("Grounded social evidence requires distinct bound actor and target IDs.", nameof(envelope));
            var expectedParticipants = new[] { Envelope.ActorId.Value, Envelope.TargetId.Value }
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (!Envelope.ParticipantIds.SequenceEqual(expectedParticipants))
                throw new ArgumentException("Social participants must contain actor and target exactly once.", nameof(envelope));
            if (Envelope.OutcomeState != RimWorldEvidenceOutcomeState.Observed)
                throw new ArgumentException("The first social semantic adapter accepts observed facts only.", nameof(envelope));
            if (Envelope.PrivacyDomain != RimWorldEvidencePrivacyDomain.RelationshipPrivate)
                throw new ArgumentException("Social semantic evidence must remain relationship-private.", nameof(envelope));
            if (!string.Equals(Envelope.SourceAdapter, GroundedSocialProjectionHash.SourceAdapter, StringComparison.Ordinal))
                throw new ArgumentException("Unexpected source adapter.", nameof(envelope));

            var payload = GroundedSocialProjectionHash.NormalizePayload(factualPayload);
            var expectedDeduplicationKey = GroundedSocialProjectionHash.ComputePr10DeduplicationKey(
                Envelope.EventId,
                Envelope.EventKind,
                Envelope.Tick,
                Envelope.ActorId.Value,
                Envelope.TargetId.Value,
                factualPayload);
            if (!string.Equals(expectedDeduplicationKey, Envelope.DeduplicationKey, StringComparison.Ordinal))
                throw new ArgumentException("The semantic payload does not match the PR #10 deduplication key.", nameof(factualPayload));

            SourceAdmissionPosition = sourceAdmissionPosition;
            EventId = Envelope.EventId;
            Tick = Envelope.Tick;
            PerspectiveOwnerId = Envelope.ActorId.Value;
            CounterpartId = Envelope.TargetId.Value;
            DeduplicationKey = Envelope.DeduplicationKey;
            SourceEnvelopeFingerprint = ComputeSourceFingerprint(payload);

            if (string.Equals(Envelope.EventKind, GroundedSocialProjectionHash.OpinionChanged, StringComparison.Ordinal))
            {
                Kind = GroundedSocialChangeKind.OpinionChanged;
                ValidateKeys(payload, OpinionRequired, "opinion");
                OpinionBefore = ParseCanonicalInt(payload["opinion_before"], -100, 100, "opinion_before");
                OpinionAfter = ParseCanonicalInt(payload["opinion_after"], -100, 100, "opinion_after");
                OpinionDelta = ParseCanonicalInt(payload["opinion_delta"], -200, 200, "opinion_delta");
                if (OpinionDelta.Value == 0 || OpinionAfter.Value - OpinionBefore.Value != OpinionDelta.Value)
                    throw new ArgumentException("Opinion delta must be nonzero and equal after-before.", nameof(factualPayload));
                RelationsBefore = Array.Empty<string>();
                RelationsAfter = Array.Empty<string>();
            }
            else if (string.Equals(Envelope.EventKind, GroundedSocialProjectionHash.DirectRelationshipChanged, StringComparison.Ordinal))
            {
                Kind = GroundedSocialChangeKind.DirectRelationshipChanged;
                ValidateKeys(payload, RelationRequired, "direct relationship");
                OpinionBefore = null;
                OpinionAfter = null;
                OpinionDelta = null;
                RelationsBefore = ParseRelations(payload["relations_before"]);
                RelationsAfter = ParseRelations(payload["relations_after"]);
                if (RelationsBefore.SequenceEqual(RelationsAfter, StringComparer.Ordinal))
                    throw new ArgumentException("Direct relationship evidence must change the relation set.", nameof(factualPayload));
            }
            else
            {
                throw new ArgumentException("Unsupported social event kind.", nameof(envelope));
            }

            GroundedSocialProjectionHash.RequireText(payload["target_external_id"], nameof(factualPayload), 256);
            if (payload.TryGetValue("pawn_external_id", out var pawnExternalId))
                GroundedSocialProjectionHash.RequireText(pawnExternalId, nameof(factualPayload), 256);

            SemanticPayloadHash = ComputeSemanticPayloadHash();
            SemanticId = ComputeSemanticId();
        }

        public RimWorldEventEvidenceEnvelope Envelope { get; }
        public EventId EventId { get; }
        public GroundedSocialChangeKind Kind { get; }
        public long Tick { get; }
        public long SourceAdmissionPosition { get; }
        public string ObservationBatchId { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public IndividualId CounterpartId { get; }
        public int? OpinionBefore { get; }
        public int? OpinionAfter { get; }
        public int? OpinionDelta { get; }
        public IReadOnlyList<string> RelationsBefore { get; }
        public IReadOnlyList<string> RelationsAfter { get; }
        public string DeduplicationKey { get; }
        public string SemanticPayloadHash { get; }
        public string SourceEnvelopeFingerprint { get; }
        public string SemanticId { get; }
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;

        private static void ValidateKeys(
            IReadOnlyDictionary<string, string> payload,
            HashSet<string> required,
            string kind)
        {
            var semanticKeys = payload.Keys
                .Where(key => !GroundedSocialProjectionHash.IsDisplayLabelKey(key))
                .ToArray();
            if (required.Any(key => !semanticKeys.Contains(key, StringComparer.Ordinal)) ||
                semanticKeys.Any(key => !required.Contains(key) && !OptionalProvenance.Contains(key)))
                throw new ArgumentException($"{kind} payload has missing or foreign semantic keys.", nameof(payload));
        }

        private static int ParseCanonicalInt(string value, int minimum, int maximum, string name)
        {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ||
                !string.Equals(parsed.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal) ||
                parsed < minimum || parsed > maximum)
                throw new ArgumentException($"{name} must be a canonical integer within bounds.", name);
            return parsed;
        }

        private static IReadOnlyList<string> ParseRelations(string value)
        {
            if (string.Equals(value, "none", StringComparison.Ordinal))
                return Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("An empty relation set must use the canonical 'none' sentinel.", nameof(value));
            if (value.IndexOf('|') >= 0 || value.IndexOf('[') >= 0 || value.IndexOf(']') >= 0 ||
                value.IndexOf('{') >= 0 || value.IndexOf('}') >= 0)
                throw new ArgumentException("Relations must use canonical comma-separated tokens.", nameof(value));
            var tokens = value.Split(',').Select(token => token.Trim()).ToArray();
            if (tokens.Any(token => string.IsNullOrWhiteSpace(token) || token.Length > 128) ||
                tokens.Any(token => string.Equals(token, "none", StringComparison.OrdinalIgnoreCase)) ||
                tokens.Length > 16 ||
                tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tokens.Length)
                throw new ArgumentException("Relation tokens must be bounded, unique, and cannot mix 'none'.", nameof(value));
            var canonical = tokens
                .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                .ThenBy(token => token, StringComparer.Ordinal)
                .ToArray();
            if (!tokens.SequenceEqual(canonical, StringComparer.Ordinal))
                throw new ArgumentException("Relation tokens must already use canonical ordering.", nameof(value));
            return new ReadOnlyCollection<string>(canonical);
        }

        private string ComputeSemanticPayloadHash()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Pair("event_kind", Envelope.EventKind),
                Pair("opinion_before", OpinionBefore?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                Pair("opinion_after", OpinionAfter?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                Pair("opinion_delta", OpinionDelta?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                Pair("relations_before", string.Join(",", RelationsBefore)),
                Pair("relations_after", string.Join(",", RelationsAfter))
            };
            return GroundedSocialProjectionHash.HashFields(fields);
        }

        private string ComputeSemanticId() => GroundedSocialProjectionHash.HashFields(new[]
        {
            Pair("event_id", EventId.ToString()),
            Pair("event_kind", Envelope.EventKind),
            Pair("tick", Tick.ToString(CultureInfo.InvariantCulture)),
            Pair("source_admission_position", SourceAdmissionPosition.ToString(CultureInfo.InvariantCulture)),
            Pair("observation_batch_id", ObservationBatchId),
            Pair("perspective_owner_id", PerspectiveOwnerId.ToString()),
            Pair("counterpart_id", CounterpartId.ToString()),
            Pair("semantic_payload_hash", SemanticPayloadHash)
        });

        private string ComputeSourceFingerprint(IReadOnlyDictionary<string, string> payload)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Pair("event_id", EventId.ToString()),
                Pair("event_kind", Envelope.EventKind),
                Pair("tick", Tick.ToString(CultureInfo.InvariantCulture)),
                Pair("deduplication_key", DeduplicationKey)
            };
            fields.AddRange(payload.Select(pair => Pair("payload:" + pair.Key, pair.Value)));
            return GroundedSocialProjectionHash.HashFields(fields);
        }

        internal static KeyValuePair<string, string> Pair(string key, string value) =>
            new KeyValuePair<string, string>(key, value);
    }

    public sealed class GroundedSocialObservationBundle
    {
        internal GroundedSocialObservationBundle(IReadOnlyList<GroundedSocialSemanticEvidence> changes)
        {
            if (changes is null || changes.Count < 1 || changes.Count > 2)
                throw new ArgumentOutOfRangeException(nameof(changes));
            var ordered = changes
                .OrderBy(value => value.Kind)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (ordered.Select(value => value.EventId).Distinct().Count() != ordered.Length)
                throw new ArgumentException("Bundle event IDs must be unique.", nameof(changes));
            if (ordered.Select(value => value.SourceAdmissionPosition).Distinct().Count() != ordered.Length)
                throw new ArgumentException("Bundle source positions must be unique.", nameof(changes));
            if (ordered.Select(value => value.Kind).Distinct().Count() != ordered.Length)
                throw new ArgumentException("Bundle cannot contain two changes of the same kind.", nameof(changes));
            var first = ordered[0];
            if (ordered.Any(value =>
                !string.Equals(value.ObservationBatchId, first.ObservationBatchId, StringComparison.Ordinal) ||
                value.Tick != first.Tick ||
                value.PerspectiveOwnerId != first.PerspectiveOwnerId ||
                value.CounterpartId != first.CounterpartId))
                throw new ArgumentException("One observation batch cannot mix ticks, owners, or counterparts.", nameof(changes));

            ObservationBatchId = first.ObservationBatchId;
            Tick = first.Tick;
            PerspectiveOwnerId = first.PerspectiveOwnerId;
            CounterpartId = first.CounterpartId;
            Changes = new ReadOnlyCollection<GroundedSocialSemanticEvidence>(ordered);
            SourceEvidenceIds = new ReadOnlyCollection<EventId>(ordered
                .Select(value => value.EventId)
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray());
            SourceAdmissionPositions = new ReadOnlyCollection<long>(ordered
                .Select(value => value.SourceAdmissionPosition)
                .OrderBy(value => value)
                .ToArray());
            CommitAdmissionPosition = SourceAdmissionPositions.Max();
            BundleId = ComputeBundleId();
        }

        public string BundleId { get; }
        public string ObservationBatchId { get; }
        public long Tick { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public IndividualId CounterpartId { get; }
        public IReadOnlyList<GroundedSocialSemanticEvidence> Changes { get; }
        public IReadOnlyList<EventId> SourceEvidenceIds { get; }
        public IReadOnlyList<long> SourceAdmissionPositions { get; }
        public long CommitAdmissionPosition { get; }
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;

        private string ComputeBundleId()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                GroundedSocialSemanticEvidence.Pair("observation_batch_id", ObservationBatchId),
                GroundedSocialSemanticEvidence.Pair("tick", Tick.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("owner", PerspectiveOwnerId.ToString()),
                GroundedSocialSemanticEvidence.Pair("counterpart", CounterpartId.ToString()),
                GroundedSocialSemanticEvidence.Pair("commit_position", CommitAdmissionPosition.ToString(CultureInfo.InvariantCulture))
            };
            foreach (var change in Changes)
                fields.Add(GroundedSocialSemanticEvidence.Pair("change", change.SemanticId));
            foreach (var evidence in SourceEvidenceIds)
                fields.Add(GroundedSocialSemanticEvidence.Pair("evidence", evidence.ToString()));
            foreach (var position in SourceAdmissionPositions)
                fields.Add(GroundedSocialSemanticEvidence.Pair("position", position.ToString(CultureInfo.InvariantCulture)));
            return GroundedSocialProjectionHash.HashFields(fields);
        }
    }

    public static class GroundedSocialObservationBundler
    {
        public static IReadOnlyList<GroundedSocialObservationBundle> Bundle(
            IEnumerable<GroundedSocialSemanticEvidence> evidence)
        {
            var values = (evidence ?? throw new ArgumentNullException(nameof(evidence))).ToArray();
            if (values.Any(value => value is null))
                throw new ArgumentException("Social evidence cannot contain null.", nameof(evidence));
            if (values.Select(value => value.EventId).Distinct().Count() != values.Length)
                throw new ArgumentException("Social event IDs must be globally unique.", nameof(evidence));
            if (values.Select(value => value.SourceAdmissionPosition).Distinct().Count() != values.Length)
                throw new ArgumentException("Social source positions must be globally unique.", nameof(evidence));
            return new ReadOnlyCollection<GroundedSocialObservationBundle>(values
                .GroupBy(value => value.ObservationBatchId, StringComparer.Ordinal)
                .Select(group => new GroundedSocialObservationBundle(group.ToArray()))
                .OrderBy(value => value.CommitAdmissionPosition)
                .ThenBy(value => value.BundleId, StringComparer.Ordinal)
                .ToArray());
        }
    }

    public sealed class GroundedSocialAppraisalProposal
    {
        internal GroundedSocialAppraisalProposal(
            string appraisalId,
            GroundedSocialObservationBundle bundle,
            MosaicEmotionFamily primaryFamily,
            MosaicEmotionFamily? secondaryFamily,
            int intensity,
            string visibleReactionSeed,
            string laterStoryConsequence,
            IEnumerable<string> stableRuleIds,
            FixedAffectVector affectDelta)
        {
            AppraisalId = GroundedSocialProjectionHash.LowerHex(appraisalId, nameof(appraisalId));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (!Enum.IsDefined(typeof(MosaicEmotionFamily), primaryFamily))
                throw new ArgumentOutOfRangeException(nameof(primaryFamily));
            if (secondaryFamily.HasValue && !Enum.IsDefined(typeof(MosaicEmotionFamily), secondaryFamily.Value))
                throw new ArgumentOutOfRangeException(nameof(secondaryFamily));
            if (intensity < 1 || intensity > 5) throw new ArgumentOutOfRangeException(nameof(intensity));
            var rules = (stableRuleIds ?? throw new ArgumentNullException(nameof(stableRuleIds)))
                .Select(value => GroundedSocialProjectionHash.RequireText(value, nameof(stableRuleIds), 128))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (rules.Length < 1 || rules.Length > 32)
                throw new ArgumentOutOfRangeException(nameof(stableRuleIds));
            PrimaryFamily = primaryFamily;
            SecondaryFamily = secondaryFamily;
            Intensity = intensity;
            VisibleReactionSeed = GroundedSocialProjectionHash.RequireText(visibleReactionSeed, nameof(visibleReactionSeed), 256);
            LaterStoryConsequence = GroundedSocialProjectionHash.RequireText(laterStoryConsequence, nameof(laterStoryConsequence), 512);
            StableRuleIds = new ReadOnlyCollection<string>(rules);
            AffectDelta = affectDelta ?? throw new ArgumentNullException(nameof(affectDelta));
        }

        public string AppraisalId { get; }
        public GroundedSocialObservationBundle Bundle { get; }
        public MosaicEmotionFamily PrimaryFamily { get; }
        public MosaicEmotionFamily? SecondaryFamily { get; }
        public int Intensity { get; }
        public string VisibleReactionSeed { get; }
        public string LaterStoryConsequence { get; }
        public IReadOnlyList<string> StableRuleIds { get; }
        public FixedAffectVector AffectDelta { get; }
        public IReadOnlyList<EventId> SourceEvidenceIds => Bundle.SourceEvidenceIds;
        public IndividualId PerspectiveOwnerId => Bundle.PerspectiveOwnerId;
        public IndividualId CounterpartId => Bundle.CounterpartId;
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;
    }

    public static class GroundedSocialAppraisalPolicy
    {
        private sealed class RelationMeaning
        {
            public RelationMeaning(MosaicEmotionFamily primary, MosaicEmotionFamily secondary, int intensity)
            {
                Primary = primary;
                Secondary = secondary;
                Intensity = intensity;
            }
            public MosaicEmotionFamily Primary { get; }
            public MosaicEmotionFamily Secondary { get; }
            public int Intensity { get; }
        }

        private static readonly IReadOnlyDictionary<string, RelationMeaning> RelationMeanings =
            new ReadOnlyDictionary<string, RelationMeaning>(new Dictionary<string, RelationMeaning>(StringComparer.Ordinal)
            {
                ["added:friend"] = new RelationMeaning(MosaicEmotionFamily.Affection, MosaicEmotionFamily.Trust, 3),
                ["removed:friend"] = new RelationMeaning(MosaicEmotionFamily.NegativeWellbeing, MosaicEmotionFamily.Uncertainty, 3),
                ["added:bond"] = new RelationMeaning(MosaicEmotionFamily.Affection, MosaicEmotionFamily.Trust, 4),
                ["removed:bond"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.NegativeWellbeing, 4),
                ["added:lover"] = new RelationMeaning(MosaicEmotionFamily.Love, MosaicEmotionFamily.Trust, 5),
                ["removed:lover"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.NegativeWellbeing, 5),
                ["added:fiance"] = new RelationMeaning(MosaicEmotionFamily.Love, MosaicEmotionFamily.Trust, 5),
                ["removed:fiance"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.NegativeWellbeing, 5),
                ["added:spouse"] = new RelationMeaning(MosaicEmotionFamily.Love, MosaicEmotionFamily.Trust, 5),
                ["removed:spouse"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.NegativeWellbeing, 5),
                ["added:exlover"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.Uncertainty, 4),
                ["added:exspouse"] = new RelationMeaning(MosaicEmotionFamily.Heartbreak, MosaicEmotionFamily.Uncertainty, 4),
                ["removed:exlover"] = new RelationMeaning(MosaicEmotionFamily.Ambiguity, MosaicEmotionFamily.Uncertainty, 2),
                ["removed:exspouse"] = new RelationMeaning(MosaicEmotionFamily.Ambiguity, MosaicEmotionFamily.Uncertainty, 2)
            });

        public static GroundedSocialAppraisalProposal Appraise(GroundedSocialObservationBundle bundle)
        {
            if (bundle is null) throw new ArgumentNullException(nameof(bundle));
            var opinion = bundle.Changes.FirstOrDefault(value => value.Kind == GroundedSocialChangeKind.OpinionChanged);
            var relation = bundle.Changes.FirstOrDefault(value => value.Kind == GroundedSocialChangeKind.DirectRelationshipChanged);
            var rules = new List<string> { "SOCIAL-APPRAISAL:ACTOR-PERSPECTIVE-ONLY:v1" };
            MosaicEmotionFamily primary;
            MosaicEmotionFamily? secondary;
            int intensity;

            if (relation is not null)
            {
                var relationResult = AppraiseRelation(relation);
                primary = relationResult.Primary;
                secondary = relationResult.Secondary;
                intensity = relationResult.Intensity;
                rules.AddRange(relationResult.Rules);
                if (opinion is not null)
                {
                    var opinionFamily = opinion.OpinionDelta!.Value > 0
                        ? MosaicEmotionFamily.Affection
                        : MosaicEmotionFamily.Resentment;
                    var opinionIntensity = OpinionIntensity(opinion.OpinionDelta.Value);
                    rules.Add(opinion.OpinionDelta.Value > 0 ? "OPINION:DELTA:POSITIVE" : "OPINION:DELTA:NEGATIVE");
                    if (primary == MosaicEmotionFamily.Ambiguity)
                    {
                        secondary = opinionFamily;
                        intensity = Math.Max(intensity, opinionIntensity);
                    }
                    else if ((IsPositive(primary) && opinion.OpinionDelta.Value < 0) ||
                             (IsNegative(primary) && opinion.OpinionDelta.Value > 0))
                    {
                        secondary = primary;
                        primary = MosaicEmotionFamily.Ambiguity;
                        intensity = Math.Max(2, Math.Max(intensity, opinionIntensity));
                        rules.Add("SOCIAL-APPRAISAL:CONTRADICTORY-SAME-TICK");
                    }
                    else
                    {
                        intensity = Math.Max(intensity, opinionIntensity);
                        if (!secondary.HasValue) secondary = opinionFamily;
                    }
                }
                rules.Add("SOCIAL-APPRAISAL:RELATION-ANCHOR-PRECEDENCE");
            }
            else if (opinion is not null)
            {
                intensity = OpinionIntensity(opinion.OpinionDelta!.Value);
                if (opinion.OpinionDelta.Value > 0)
                {
                    primary = MosaicEmotionFamily.Affection;
                    secondary = MosaicEmotionFamily.PositiveWellbeing;
                    rules.Add("OPINION:DELTA:POSITIVE");
                }
                else
                {
                    primary = MosaicEmotionFamily.Resentment;
                    secondary = MosaicEmotionFamily.NegativeWellbeing;
                    rules.Add("OPINION:DELTA:NEGATIVE");
                }
            }
            else
            {
                throw new ArgumentException("Bundle contains no appraisable social change.", nameof(bundle));
            }

            var reaction = VisibleReaction(primary);
            var consequence = "This observed change may influence later relationship interpretation, but cannot itself rewrite the relationship or issue an action.";
            var delta = FixedAffectVector.FromFamilies(primary, secondary, intensity);
            var fields = new List<KeyValuePair<string, string>>
            {
                GroundedSocialSemanticEvidence.Pair("bundle_id", bundle.BundleId),
                GroundedSocialSemanticEvidence.Pair("owner", bundle.PerspectiveOwnerId.ToString()),
                GroundedSocialSemanticEvidence.Pair("counterpart", bundle.CounterpartId.ToString()),
                GroundedSocialSemanticEvidence.Pair("primary", primary.ToString()),
                GroundedSocialSemanticEvidence.Pair("secondary", secondary?.ToString() ?? "null"),
                GroundedSocialSemanticEvidence.Pair("intensity", intensity.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("affect", delta.Fingerprint())
            };
            foreach (var rule in rules.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                fields.Add(GroundedSocialSemanticEvidence.Pair("rule", rule));
            var appraisalId = GroundedSocialProjectionHash.HashFields(fields);
            return new GroundedSocialAppraisalProposal(
                appraisalId,
                bundle,
                primary,
                secondary,
                intensity,
                reaction,
                consequence,
                rules,
                delta);
        }

        private static (MosaicEmotionFamily Primary, MosaicEmotionFamily Secondary, int Intensity, IReadOnlyList<string> Rules)
            AppraiseRelation(GroundedSocialSemanticEvidence relation)
        {
            var before = new HashSet<string>(relation.RelationsBefore, StringComparer.OrdinalIgnoreCase);
            var after = new HashSet<string>(relation.RelationsAfter, StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(RelationMeaning Meaning, string Rule)>();
            foreach (var token in after.Except(before, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                candidates.Add(RelationCandidate("added", token));
            foreach (var token in before.Except(after, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                candidates.Add(RelationCandidate("removed", token));
            if (candidates.Count == 0)
                return (MosaicEmotionFamily.Ambiguity, MosaicEmotionFamily.Uncertainty, 1, new[] { "RELATION:UNINTERPRETABLE" });
            candidates = candidates
                .OrderByDescending(value => value.Meaning.Intensity)
                .ThenBy(value => value.Meaning.Primary)
                .ThenBy(value => value.Rule, StringComparer.Ordinal)
                .ToList();
            var hasPositive = candidates.Any(value => IsPositive(value.Meaning.Primary));
            var hasNegative = candidates.Any(value => IsNegative(value.Meaning.Primary));
            var dominant = candidates[0];
            if (hasPositive && hasNegative)
                return (MosaicEmotionFamily.Ambiguity, dominant.Meaning.Primary, Math.Max(2, dominant.Meaning.Intensity), candidates.Select(value => value.Rule).ToArray());
            return (dominant.Meaning.Primary, dominant.Meaning.Secondary, dominant.Meaning.Intensity, candidates.Select(value => value.Rule).ToArray());
        }

        private static (RelationMeaning Meaning, string Rule) RelationCandidate(string transition, string token)
        {
            var key = transition + ":" + token.ToLowerInvariant();
            if (RelationMeanings.TryGetValue(key, out var meaning))
                return (meaning, "RELATION:" + transition.ToUpperInvariant() + ":" + token.ToLowerInvariant());
            return (
                new RelationMeaning(MosaicEmotionFamily.Ambiguity, MosaicEmotionFamily.Uncertainty, 1),
                "RELATION:" + transition.ToUpperInvariant() + ":UNCLASSIFIED:" + token.ToLowerInvariant());
        }

        private static int OpinionIntensity(int delta)
        {
            var magnitude = Math.Abs(delta);
            if (magnitude <= 4) return 1;
            if (magnitude <= 14) return 2;
            if (magnitude <= 29) return 3;
            if (magnitude <= 59) return 4;
            return 5;
        }

        private static bool IsPositive(MosaicEmotionFamily family) =>
            family == MosaicEmotionFamily.Affection || family == MosaicEmotionFamily.Trust ||
            family == MosaicEmotionFamily.Love || family == MosaicEmotionFamily.PositiveWellbeing;

        private static bool IsNegative(MosaicEmotionFamily family) =>
            family == MosaicEmotionFamily.Resentment || family == MosaicEmotionFamily.Suspicion ||
            family == MosaicEmotionFamily.Heartbreak || family == MosaicEmotionFamily.NegativeWellbeing;

        private static string VisibleReaction(MosaicEmotionFamily family)
        {
            switch (family)
            {
                case MosaicEmotionFamily.Affection: return "Warmly affected";
                case MosaicEmotionFamily.Love: return "Deeply attached";
                case MosaicEmotionFamily.Trust: return "Reassured";
                case MosaicEmotionFamily.Resentment: return "Cooler and resentful";
                case MosaicEmotionFamily.Suspicion: return "Wary";
                case MosaicEmotionFamily.Heartbreak: return "Hurt by the lost bond";
                case MosaicEmotionFamily.PositiveWellbeing: return "Relieved";
                case MosaicEmotionFamily.NegativeWellbeing: return "Distressed";
                case MosaicEmotionFamily.Ambiguity: return "Conflicted";
                default: return "Affected";
            }
        }
    }
}
