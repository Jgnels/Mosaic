using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dagmay.Core.Development
{
    public static class GroundedDevelopmentalContextSource
    {
        public const string Contract = "Mosaic.Core.GroundedDevelopmentalContextMaterialization.v1";
        public const string FormalGateDigest = "c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9";
        public const string V41FormalGateDigest = "7eef7fe095621a15b2e84464abcabf2d05ba3681d41d51488317c2d73776755d";
        public const string V41ExecutableGateDigest = "9fa64475517509764ef306ad6c351ce886b6be6750281889145df320848c1b2b";
        public const string V40GateDigest = "639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9";
        public const string MemoryEvidenceContract = "Mosaic.Core.ExistingMemoryEvidence.v1";
        public const string Authority = "READ_ONLY_APPRAISAL_CONTEXT_NO_CANONICAL_PROVIDER_PLANNER_UI_OR_PAWN_AUTHORITY";
        public const int MaximumItems = 4;
        public const int MaximumCanonicalDelta = 1_000_000;
    }

    public sealed class GroundedDimensionContribution
    {
        internal GroundedDimensionContribution(string dimension, int valueBps)
        {
            Dimension = RetrievalCanonical.Token(dimension, nameof(dimension));
            ValueBps = valueBps;
        }

        public string Dimension { get; }
        public int ValueBps { get; }
    }

    public sealed class GroundedDevelopmentalContextItem
    {
        internal GroundedDevelopmentalContextItem(
            DevelopmentalContextRole role,
            DevelopmentalEvidenceKind kind,
            string evidenceId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long eventTick,
            string category,
            IEnumerable<string> reasonCodes,
            int contributionCapBps,
            IEnumerable<GroundedDimensionContribution> allocatedDimensionsBps,
            int salience,
            int durability,
            string projectionFingerprint)
        {
            Role = role;
            Kind = kind;
            EvidenceId = RetrievalCanonical.Text(evidenceId, nameof(evidenceId));
            SourceEventId = RetrievalCanonical.Text(sourceEventId, nameof(sourceEventId));
            RootEventIds = RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8);
            if (eventTick < 0) throw new ArgumentOutOfRangeException(nameof(eventTick));
            EventTick = eventTick;
            Category = RetrievalCanonical.Token(category, nameof(category));
            ReasonCodes = RetrievalCanonical.TokenSet(reasonCodes, nameof(reasonCodes), 8);
            if (contributionCapBps < 0 || contributionCapBps > 4_000)
                throw new ArgumentOutOfRangeException(nameof(contributionCapBps));
            ContributionCapBps = contributionCapBps;
            AllocatedDimensionsBps = new ReadOnlyCollection<GroundedDimensionContribution>(
                (allocatedDimensionsBps ?? throw new ArgumentNullException(nameof(allocatedDimensionsBps)))
                    .OrderBy(value => value.Dimension, StringComparer.Ordinal)
                    .ToList());
            if (AllocatedDimensionsBps.Select(value => value.Dimension)
                    .Distinct(StringComparer.Ordinal).Count() != AllocatedDimensionsBps.Count ||
                AllocatedDimensionsBps.Sum(value => Math.Abs(value.ValueBps)) > ContributionCapBps)
                throw new ArgumentException("Allocated dimensions exceed the role cap.", nameof(allocatedDimensionsBps));
            if (salience < 0 || salience > 100 || durability < 0 || durability > 100)
                throw new ArgumentOutOfRangeException(nameof(salience));
            Salience = salience;
            Durability = durability;
            ProjectionFingerprint = RetrievalCanonical.Hex(projectionFingerprint, nameof(projectionFingerprint));
        }

        public DevelopmentalContextRole Role { get; }
        public DevelopmentalEvidenceKind Kind { get; }
        public string EvidenceId { get; }
        public string SourceEventId { get; }
        public IReadOnlyList<string> RootEventIds { get; }
        public long EventTick { get; }
        public string Category { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public int ContributionCapBps { get; }
        public IReadOnlyList<GroundedDimensionContribution> AllocatedDimensionsBps { get; }
        public int Salience { get; }
        public int Durability { get; }
        public string ProjectionFingerprint { get; }

        internal string DeterministicJson() =>
            "{" +
            "\"allocated_dimensions_bps\":" +
                GroundedJson.IntPairs(AllocatedDimensionsBps.Select(value =>
                    new KeyValuePair<string, int>(value.Dimension, value.ValueBps))) + "," +
            "\"category\":" + GroundedJson.String(Category) + "," +
            "\"contribution_cap_bps\":" + GroundedJson.Number(ContributionCapBps) + "," +
            "\"durability\":" + GroundedJson.Number(Durability) + "," +
            "\"event_tick\":" + GroundedJson.Number(EventTick) + "," +
            "\"evidence_id\":" + GroundedJson.String(EvidenceId) + "," +
            "\"kind\":" + GroundedJson.String(GroundedTokens.Kind(Kind)) + "," +
            "\"projection_fingerprint\":" + GroundedJson.String(ProjectionFingerprint) + "," +
            "\"reason_codes\":" + GroundedJson.Strings(ReasonCodes) + "," +
            "\"role\":" + GroundedJson.String(GroundedTokens.Role(Role)) + "," +
            "\"root_event_ids\":" + GroundedJson.Strings(RootEventIds) + "," +
            "\"salience\":" + GroundedJson.Number(Salience) + "," +
            "\"source_event_id\":" + GroundedJson.String(SourceEventId) +
            "}";
    }

    public sealed class GroundedDimensionSynthesis
    {
        internal GroundedDimensionSynthesis(
            string dimension,
            int netEvidenceBps,
            int positiveEvidenceBps,
            int negativeEvidenceBps,
            int sourceCount,
            int contradictionCount,
            int confidenceBps)
        {
            Dimension = RetrievalCanonical.Token(dimension, nameof(dimension));
            NetEvidenceBps = netEvidenceBps;
            PositiveEvidenceBps = positiveEvidenceBps;
            NegativeEvidenceBps = negativeEvidenceBps;
            SourceCount = sourceCount;
            ContradictionCount = contradictionCount;
            ConfidenceBps = confidenceBps;
            if (positiveEvidenceBps < 0 || negativeEvidenceBps < 0 ||
                sourceCount < 1 || contradictionCount < 0 ||
                confidenceBps < 0 || confidenceBps > 10_000)
                throw new ArgumentOutOfRangeException(nameof(positiveEvidenceBps));
        }

        public string Dimension { get; }
        public int NetEvidenceBps { get; }
        public int PositiveEvidenceBps { get; }
        public int NegativeEvidenceBps { get; }
        public int SourceCount { get; }
        public int ContradictionCount { get; }
        public int ConfidenceBps { get; }

        internal string DeterministicJson() =>
            "{" +
            "\"confidence_bps\":" + GroundedJson.Number(ConfidenceBps) + "," +
            "\"contradiction_count\":" + GroundedJson.Number(ContradictionCount) + "," +
            "\"dimension\":" + GroundedJson.String(Dimension) + "," +
            "\"negative_evidence_bps\":" + GroundedJson.Number(NegativeEvidenceBps) + "," +
            "\"net_evidence_bps\":" + GroundedJson.Number(NetEvidenceBps) + "," +
            "\"positive_evidence_bps\":" + GroundedJson.Number(PositiveEvidenceBps) + "," +
            "\"source_count\":" + GroundedJson.Number(SourceCount) +
            "}";
    }

    public sealed class GroundedDevelopmentalContextPacket
    {
        internal GroundedDevelopmentalContextPacket(
            string ownerId,
            string lineageId,
            string? counterpartId,
            DevelopmentalContextPurpose purpose,
            string requestFingerprint,
            string v41BundleFingerprint,
            IEnumerable<GroundedDevelopmentalContextItem> items,
            IEnumerable<GroundedDimensionSynthesis> dimensions,
            IEnumerable<string> uncertaintyCodes,
            IEnumerable<KeyValuePair<string, long>> metrics)
        {
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            Purpose = purpose;
            RequestFingerprint = RetrievalCanonical.Hex(requestFingerprint, nameof(requestFingerprint));
            V41BundleFingerprint = RetrievalCanonical.Hex(v41BundleFingerprint, nameof(v41BundleFingerprint));
            Items = new ReadOnlyCollection<GroundedDevelopmentalContextItem>(
                (items ?? throw new ArgumentNullException(nameof(items))).ToList());
            if (Items.Count > GroundedDevelopmentalContextSource.MaximumItems)
                throw new ArgumentOutOfRangeException(nameof(items));
            Dimensions = new ReadOnlyCollection<GroundedDimensionSynthesis>(
                (dimensions ?? throw new ArgumentNullException(nameof(dimensions)))
                    .OrderBy(value => value.Dimension, StringComparer.Ordinal).ToList());
            UncertaintyCodes = RetrievalCanonical.TokenSet(
                uncertaintyCodes ?? throw new ArgumentNullException(nameof(uncertaintyCodes)),
                nameof(uncertaintyCodes),
                16);
            Metrics = new ReadOnlyDictionary<string, long>(
                (metrics ?? throw new ArgumentNullException(nameof(metrics)))
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToDictionary(value => RetrievalCanonical.Token(value.Key, nameof(metrics)),
                                  value => value.Value, StringComparer.Ordinal));
            SourceContract = GroundedDevelopmentalContextSource.Contract;
            SourceGateDigest = GroundedDevelopmentalContextSource.FormalGateDigest;
            Authority = GroundedDevelopmentalContextSource.Authority;
            Fingerprint = GroundedJson.Hash(DeterministicJson(false));
        }

        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public DevelopmentalContextPurpose Purpose { get; }
        public string RequestFingerprint { get; }
        public string V41BundleFingerprint { get; }
        public IReadOnlyList<GroundedDevelopmentalContextItem> Items { get; }
        public IReadOnlyList<GroundedDimensionSynthesis> Dimensions { get; }
        public IReadOnlyList<string> UncertaintyCodes { get; }
        public IReadOnlyDictionary<string, long> Metrics { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string Authority { get; }
        public string Fingerprint { get; private set; }

        public string ToDeterministicJson() => DeterministicJson(true);

        public bool VerifyFingerprint() =>
            string.Equals(Fingerprint, GroundedJson.Hash(DeterministicJson(false)), StringComparison.Ordinal);

        public static GroundedDevelopmentalContextPacket RequireValid(
            GroundedDevelopmentalContextPacket packet)
        {
            if (packet is null) throw new ArgumentNullException(nameof(packet));
            if (!string.Equals(packet.SourceContract, GroundedDevelopmentalContextSource.Contract, StringComparison.Ordinal) ||
                !string.Equals(packet.SourceGateDigest, GroundedDevelopmentalContextSource.FormalGateDigest, StringComparison.Ordinal) ||
                !string.Equals(packet.Authority, GroundedDevelopmentalContextSource.Authority, StringComparison.Ordinal) ||
                !packet.VerifyFingerprint())
                throw new ArgumentException("Grounded developmental context packet failed public self-verification.", nameof(packet));
            return packet;
        }

        private string DeterministicJson(bool includeFingerprint)
        {
            var fields = new List<string>
            {
                "\"authority\":" + GroundedJson.String(Authority),
                "\"counterpart_id\":" + GroundedJson.OptionalString(CounterpartId),
                "\"dimensions\":[" + string.Join(",", Dimensions.Select(value => value.DeterministicJson())) + "]"
            };
            if (includeFingerprint)
                fields.Add("\"fingerprint\":" + GroundedJson.String(Fingerprint));
            fields.Add("\"items\":[" + string.Join(",", Items.Select(value => value.DeterministicJson())) + "]");
            fields.Add("\"lineage_id\":" + GroundedJson.String(LineageId));
            fields.Add("\"metrics\":" + GroundedJson.LongPairs(Metrics));
            fields.Add("\"owner_id\":" + GroundedJson.String(OwnerId));
            fields.Add("\"purpose\":" + GroundedJson.String(GroundedTokens.Purpose(Purpose)));
            fields.Add("\"request_fingerprint\":" + GroundedJson.String(RequestFingerprint));
            fields.Add("\"source_contract\":" + GroundedJson.String(SourceContract));
            fields.Add("\"source_gate_digest\":" + GroundedJson.String(SourceGateDigest));
            fields.Add("\"uncertainty_codes\":" + GroundedJson.Strings(UncertaintyCodes));
            fields.Add("\"v41_bundle_fingerprint\":" + GroundedJson.String(V41BundleFingerprint));
            return "{" + string.Join(",", fields) + "}";
        }
    }

    internal static class GroundedTokens
    {
        internal static string Kind(DevelopmentalEvidenceKind kind) =>
            kind == DevelopmentalEvidenceKind.Developmental ? "DEVELOPMENTAL" :
            kind == DevelopmentalEvidenceKind.Memory ? "MEMORY" :
            throw new ArgumentOutOfRangeException(nameof(kind));

        internal static string Role(DevelopmentalContextRole role) => RetrievalCanonical.Role(role);

        internal static string Purpose(DevelopmentalContextPurpose purpose)
        {
            switch (purpose)
            {
                case DevelopmentalContextPurpose.Appraisal: return "APPRAISAL";
                case DevelopmentalContextPurpose.Dialogue: return "DIALOGUE";
                case DevelopmentalContextPurpose.DecisionExplanation: return "DECISION_EXPLANATION";
                default: throw new ArgumentOutOfRangeException(nameof(purpose));
            }
        }
    }

    internal static class GroundedJson
    {
        internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static string OptionalString(string? value) => value is null ? "null" : String(value);

        internal static string String(string value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            var builder = new StringBuilder(value.Length + 2).Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        internal static string Strings(IEnumerable<string> values) =>
            "[" + string.Join(",", values.Select(String)) + "]";

        internal static string IntPairs(IEnumerable<KeyValuePair<string, int>> values) =>
            "[" + string.Join(",", values.Select(value =>
                "[" + String(value.Key) + "," + Number(value.Value) + "]")) + "]";

        internal static string LongPairs(IEnumerable<KeyValuePair<string, long>> values) =>
            "[" + string.Join(",", values.Select(value =>
                "[" + String(value.Key) + "," + Number(value.Value) + "]")) + "]";

        internal static string Hash(string deterministicJson)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(deterministicJson))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
