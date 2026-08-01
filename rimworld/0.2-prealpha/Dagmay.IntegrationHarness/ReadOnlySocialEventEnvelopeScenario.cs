using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Contracts;
using Dagmay.RimWorld.Perception;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> ReadOnlySocialEventEnvelopeAsync()
        {
            var assertions = new HarnessAssert();
            var forward = ProjectSupportedFacts(reverseInput: false, reversePayload: false);
            var reverse = ProjectSupportedFacts(reverseInput: true, reversePayload: true);
            var forwardBytes = SerializeEnvelopeSet(forward);
            var reverseBytes = SerializeEnvelopeSet(reverse);

            assertions.Equal(
                2,
                forward.Count,
                "Both supported already-admitted social facts produce envelopes.");
            assertions.True(
                forward.Select(value => value.Envelope.EventKind).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[]
                    {
                        ReadOnlySocialEventEnvelopeAdapter.DirectRelationshipChanged,
                        ReadOnlySocialEventEnvelopeAdapter.OpinionChanged
                    }),
                "The integration output contains exactly the two approved event kinds.");
            assertions.True(
                forwardBytes.SequenceEqual(reverseBytes),
                "Reversing fact and payload input order produces byte-identical envelope output.");
            assertions.True(
                forward.All(value =>
                    value.WitnessIds.Count == 0 &&
                    !value.Envelope.CanClaimSuccess &&
                    !value.Envelope.DirectActionAuthority &&
                    !value.Envelope.DirectCharacterMutation),
                "Integrated envelopes remain witness-free, non-successful, and non-authoritative.");

            var metrics = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["supportedFacts"] = forward.Count,
                ["inputOrders"] = 2,
                ["outputBytes"] = forwardBytes.Length
            };
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["envelopeDigest"] = LowerSha256(forwardBytes)
            };
            return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, details));
        }

        private static IReadOnlyList<ReadOnlyRimWorldEventProjection> ProjectSupportedFacts(
            bool reverseInput,
            bool reversePayload)
        {
            var actor = IndividualId.Parse("d1000000000000000000000000000002");
            var target = IndividualId.Parse("d1000000000000000000000000000001");
            var facts = new[]
            {
                new AdapterFact(
                    EventId.Parse("d2000000000000000000000000000001"),
                    1000,
                    ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["target_external_id"] = "Thing_Target",
                        ["target_name"] = "Counterpart",
                        ["opinion_before"] = "-10",
                        ["opinion_after"] = "20",
                        ["opinion_delta"] = "30",
                        ["pawn_external_id"] = "Thing_Actor",
                        ["display_name_at_event"] = "Perspective owner"
                    }),
                new AdapterFact(
                    EventId.Parse("d2000000000000000000000000000002"),
                    1010,
                    ReadOnlySocialEventEnvelopeAdapter.DirectRelationshipChanged,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["target_external_id"] = "Thing_Target",
                        ["target_name"] = "Counterpart",
                        ["relations_before"] = "none",
                        ["relations_after"] = "Friend",
                        ["pawn_external_id"] = "Thing_Actor",
                        ["display_name_at_event"] = "Perspective owner"
                    })
            };
            var orderedFacts = reverseInput ? facts.Reverse() : facts;
            var capture = new ReadOnlySocialEventEnvelopeCapture();
            var result = new List<ReadOnlyRimWorldEventProjection>();
            foreach (var fact in orderedFacts)
            {
                var payload = reversePayload
                    ? fact.Payload.Reverse().ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal)
                    : fact.Payload;
                var projection = capture.TryCapture(
                    true,
                    true,
                    fact.EventId,
                    fact.Tick,
                    fact.EventKind,
                    payload,
                    actor,
                    target);
                if (projection is null)
                    throw new HarnessAssertionException("A supported admitted social fact failed to project.");
                result.Add(projection);
            }

            return result;
        }

        private static byte[] SerializeEnvelopeSet(
            IEnumerable<ReadOnlyRimWorldEventProjection> projections)
        {
            var builder = new StringBuilder();
            Append(builder, "schema", "mosaic.rimworld-event-adapter-fixtures.v1");
            foreach (var projection in projections
                .OrderBy(value => value.Envelope.EventId.ToString(), StringComparer.Ordinal))
            {
                var envelope = projection.Envelope;
                Append(builder, "event_id", envelope.EventId.ToString());
                Append(builder, "kind", envelope.EventKind);
                Append(builder, "tick", envelope.Tick.ToString(CultureInfo.InvariantCulture));
                Append(builder, "actor_id", envelope.ActorId!.Value.ToString());
                Append(builder, "target_id", envelope.TargetId!.Value.ToString());
                foreach (var participantId in envelope.ParticipantIds)
                    Append(builder, "participant_id", participantId.ToString());
                Append(
                    builder,
                    "witness_count",
                    projection.WitnessIds.Count.ToString(CultureInfo.InvariantCulture));
                Append(builder, "outcome", envelope.OutcomeState.ToString());
                Append(builder, "privacy", envelope.PrivacyDomain.ToString());
                Append(builder, "source_adapter", envelope.SourceAdapter);
                Append(builder, "deduplication_key", envelope.DeduplicationKey);
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
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

        private static string LowerSha256(byte[] value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(value)
                        .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private sealed class AdapterFact
        {
            public AdapterFact(
                EventId eventId,
                long tick,
                string eventKind,
                Dictionary<string, string> payload)
            {
                EventId = eventId;
                Tick = tick;
                EventKind = eventKind;
                Payload = payload;
            }

            public EventId EventId { get; }
            public long Tick { get; }
            public string EventKind { get; }
            public Dictionary<string, string> Payload { get; }
        }
    }
}
