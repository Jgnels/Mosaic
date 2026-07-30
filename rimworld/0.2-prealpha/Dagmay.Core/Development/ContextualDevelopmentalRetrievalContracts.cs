using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dagmay.Core.Development
{
    public static class ContextualDevelopmentalRetrievalSource
    {
        public const string Contract = "Mosaic.Core.ContextualDevelopmentalRetrieval.v1";
        public const string DependencyGateDigest = "639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9";
        public const string Authority = "READ_ONLY_CONTEXT_NO_CANONICAL_OR_PAWN_AUTHORITY";
        public const int MaximumCandidatesPerKey = 64;
        public const int MaximumResults = 4;
    }

    public enum DevelopmentalContextPurpose
    {
        Appraisal,
        Dialogue,
        DecisionExplanation
    }

    public enum DevelopmentalEvidenceKind
    {
        Developmental,
        Memory
    }

    public enum DevelopmentalContextRole
    {
        DurableAnchor,
        RecentLived,
        CategoryAnchor,
        ContradictoryContext
    }

    public sealed class ContextualMemoryEvidence
    {
        private ContextualMemoryEvidence(
            string memoryId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long observedTick,
            string category,
            IEnumerable<string> tags,
            int salience,
            long checkpointGeneration,
            string checkpointFingerprint,
            bool admitted,
            string privacy,
            string fingerprint)
        {
            MemoryId = RetrievalCanonical.Text(memoryId, nameof(memoryId));
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            SourceEventId = RetrievalCanonical.Text(sourceEventId, nameof(sourceEventId));
            RootEventIds = RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8);
            if (observedTick < 0) throw new ArgumentOutOfRangeException(nameof(observedTick));
            ObservedTick = observedTick;
            Category = RetrievalCanonical.Token(category, nameof(category));
            Tags = RetrievalCanonical.TokenSet(tags, nameof(tags), 16);
            if (salience < 0 || salience > 100) throw new ArgumentOutOfRangeException(nameof(salience));
            Salience = salience;
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            CheckpointFingerprint = RetrievalCanonical.Hex(checkpointFingerprint, nameof(checkpointFingerprint));
            Admitted = admitted;
            Privacy = RetrievalCanonical.Text(privacy, nameof(privacy));
            Fingerprint = RetrievalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (!string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Contextual memory fingerprint mismatch.", nameof(fingerprint));
            if (!Admitted || !string.Equals(Privacy, "OWNER_PRIVATE", StringComparison.Ordinal))
                throw new ArgumentException("Only admitted owner-private memories may be indexed.");
            if (RootEventIds.Any(value => value.StartsWith("developmental-record:", StringComparison.Ordinal)))
                throw new ArgumentException("Recursive developmental roots are forbidden.", nameof(rootEventIds));
        }

        public static ContextualMemoryEvidence Create(
            string memoryId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long observedTick,
            string category,
            IEnumerable<string> tags,
            int salience,
            long checkpointGeneration,
            string checkpointFingerprint)
        {
            var fingerprint = Compute(
                memoryId, ownerId, lineageId, counterpartId, sourceEventId, rootEventIds,
                observedTick, category, tags, salience, checkpointGeneration, checkpointFingerprint,
                true, "OWNER_PRIVATE");
            return new ContextualMemoryEvidence(
                memoryId, ownerId, lineageId, counterpartId, sourceEventId, rootEventIds,
                observedTick, category, tags, salience, checkpointGeneration, checkpointFingerprint,
                true, "OWNER_PRIVATE", fingerprint);
        }

        internal static ContextualMemoryEvidence Restore(
            string memoryId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long observedTick,
            string category,
            IEnumerable<string> tags,
            int salience,
            long checkpointGeneration,
            string checkpointFingerprint,
            bool admitted,
            string privacy,
            string fingerprint) =>
            new ContextualMemoryEvidence(
                memoryId, ownerId, lineageId, counterpartId, sourceEventId, rootEventIds,
                observedTick, category, tags, salience, checkpointGeneration, checkpointFingerprint,
                admitted, privacy, fingerprint);

        public string MemoryId { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string SourceEventId { get; }
        public IReadOnlyList<string> RootEventIds { get; }
        public long ObservedTick { get; }
        public string Category { get; }
        public IReadOnlyList<string> Tags { get; }
        public int Salience { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public bool Admitted { get; }
        public string Privacy { get; }
        public string Fingerprint { get; }

        private string ComputeFingerprint() =>
            Compute(
                MemoryId, OwnerId, LineageId, CounterpartId, SourceEventId, RootEventIds,
                ObservedTick, Category, Tags, Salience, CheckpointGeneration, CheckpointFingerprint,
                Admitted, Privacy);

        private static string Compute(
            string memoryId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long observedTick,
            string category,
            IEnumerable<string> tags,
            int salience,
            long checkpointGeneration,
            string checkpointFingerprint,
            bool admitted,
            string privacy) =>
            RetrievalCanonical.Hash(new[]
            {
                RetrievalCanonical.Pair("schema", ContextualDevelopmentalRetrievalSource.Contract),
                RetrievalCanonical.Pair("kind", "MEMORY"),
                RetrievalCanonical.Pair("memory_id", memoryId),
                RetrievalCanonical.Pair("owner_id", ownerId),
                RetrievalCanonical.Pair("lineage_id", lineageId),
                RetrievalCanonical.Pair("counterpart_id", counterpartId ?? "null"),
                RetrievalCanonical.Pair("source_event_id", sourceEventId),
                RetrievalCanonical.Pair("root_event_ids", string.Join(",", RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8))),
                RetrievalCanonical.Pair("observed_tick", observedTick.ToString(CultureInfo.InvariantCulture)),
                RetrievalCanonical.Pair("category", category),
                RetrievalCanonical.Pair("tags", string.Join(",", RetrievalCanonical.TokenSet(tags, nameof(tags), 16))),
                RetrievalCanonical.Pair("salience", salience.ToString(CultureInfo.InvariantCulture)),
                RetrievalCanonical.Pair("checkpoint_generation", checkpointGeneration.ToString(CultureInfo.InvariantCulture)),
                RetrievalCanonical.Pair("checkpoint_fingerprint", checkpointFingerprint),
                RetrievalCanonical.Pair("admitted", admitted ? "true" : "false"),
                RetrievalCanonical.Pair("privacy", privacy)
            });
    }

    public sealed class DevelopmentalContextQuery
    {
        public DevelopmentalContextQuery(
            string saveId,
            string worldId,
            string storeSetId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            long currentTick,
            long checkpointGeneration,
            IEnumerable<string> checkpointAncestry,
            DevelopmentalContextPurpose purpose,
            IEnumerable<string> contextTags,
            int maxItems = ContextualDevelopmentalRetrievalSource.MaximumResults)
        {
            SaveId = RetrievalCanonical.Text(saveId, nameof(saveId));
            WorldId = RetrievalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = RetrievalCanonical.Text(storeSetId, nameof(storeSetId));
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            if (currentTick < 0 || checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            CurrentTick = currentTick;
            CheckpointGeneration = checkpointGeneration;
            CheckpointAncestry = RetrievalCanonical.OrderedHexSet(checkpointAncestry, nameof(checkpointAncestry), 256);
            Purpose = purpose;
            ContextTags = RetrievalCanonical.TokenSet(contextTags, nameof(contextTags), 16);
            if (maxItems < 1 || maxItems > ContextualDevelopmentalRetrievalSource.MaximumResults)
                throw new ArgumentOutOfRangeException(nameof(maxItems));
            MaxItems = maxItems;
            Fingerprint = RetrievalCanonical.Hash(Fields());
        }

        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public long CurrentTick { get; }
        public long CheckpointGeneration { get; }
        public IReadOnlyList<string> CheckpointAncestry { get; }
        public DevelopmentalContextPurpose Purpose { get; }
        public IReadOnlyList<string> ContextTags { get; }
        public int MaxItems { get; }
        public string Fingerprint { get; }

        private IEnumerable<KeyValuePair<string, string>> Fields()
        {
            yield return RetrievalCanonical.Pair("schema", ContextualDevelopmentalRetrievalSource.Contract);
            yield return RetrievalCanonical.Pair("save_id", SaveId);
            yield return RetrievalCanonical.Pair("world_id", WorldId);
            yield return RetrievalCanonical.Pair("store_set_id", StoreSetId);
            yield return RetrievalCanonical.Pair("owner_id", OwnerId);
            yield return RetrievalCanonical.Pair("lineage_id", LineageId);
            yield return RetrievalCanonical.Pair("counterpart_id", CounterpartId ?? "null");
            yield return RetrievalCanonical.Pair("current_tick", CurrentTick.ToString(CultureInfo.InvariantCulture));
            yield return RetrievalCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture));
            yield return RetrievalCanonical.Pair("checkpoint_ancestry", string.Join(",", CheckpointAncestry));
            yield return RetrievalCanonical.Pair("purpose", Purpose.ToString().ToUpperInvariant());
            yield return RetrievalCanonical.Pair("context_tags", string.Join(",", ContextTags));
            yield return RetrievalCanonical.Pair("max_items", MaxItems.ToString(CultureInfo.InvariantCulture));
        }
    }

    public sealed class DevelopmentalContextItem
    {
        internal DevelopmentalContextItem(
            string evidenceId,
            DevelopmentalEvidenceKind kind,
            DevelopmentalContextRole role,
            string category,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long eventTick,
            int contributionCapBps,
            IEnumerable<string> reasonCodes)
        {
            EvidenceId = RetrievalCanonical.Text(evidenceId, nameof(evidenceId));
            Kind = kind;
            Role = role;
            Category = RetrievalCanonical.Token(category, nameof(category));
            SourceEventId = RetrievalCanonical.Text(sourceEventId, nameof(sourceEventId));
            RootEventIds = RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8);
            EventTick = eventTick;
            ContributionCapBps = contributionCapBps;
            ReasonCodes = RetrievalCanonical.TokenSet(reasonCodes, nameof(reasonCodes), 8);
            Fingerprint = RetrievalCanonical.Hash(new[]
            {
                RetrievalCanonical.Pair("evidence_id", EvidenceId),
                RetrievalCanonical.Pair("kind", Kind.ToString().ToUpperInvariant()),
                RetrievalCanonical.Pair("role", RetrievalCanonical.Role(Role)),
                RetrievalCanonical.Pair("category", Category),
                RetrievalCanonical.Pair("source_event_id", SourceEventId),
                RetrievalCanonical.Pair("root_event_ids", string.Join(",", RootEventIds)),
                RetrievalCanonical.Pair("event_tick", EventTick.ToString(CultureInfo.InvariantCulture)),
                RetrievalCanonical.Pair("contribution_cap_bps", ContributionCapBps.ToString(CultureInfo.InvariantCulture)),
                RetrievalCanonical.Pair("reason_codes", string.Join(",", ReasonCodes))
            });
        }

        public string EvidenceId { get; }
        public DevelopmentalEvidenceKind Kind { get; }
        public DevelopmentalContextRole Role { get; }
        public string Category { get; }
        public string SourceEventId { get; }
        public IReadOnlyList<string> RootEventIds { get; }
        public long EventTick { get; }
        public int ContributionCapBps { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public string Fingerprint { get; }
    }

    public sealed class DevelopmentalContextBundle
    {
        internal DevelopmentalContextBundle(
            DevelopmentalContextQuery query,
            IEnumerable<DevelopmentalContextItem> selected,
            IEnumerable<KeyValuePair<string, long>> selectionMetrics)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            SourceContract = ContextualDevelopmentalRetrievalSource.Contract;
            Authority = ContextualDevelopmentalRetrievalSource.Authority;
            OwnerId = query.OwnerId;
            LineageId = query.LineageId;
            CounterpartId = query.CounterpartId;
            Purpose = query.Purpose;
            QueryFingerprint = query.Fingerprint;
            Selected = new ReadOnlyCollection<DevelopmentalContextItem>((selected ?? throw new ArgumentNullException(nameof(selected))).ToList());
            SelectionMetrics = new ReadOnlyDictionary<string, long>(
                (selectionMetrics ?? throw new ArgumentNullException(nameof(selectionMetrics)))
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
            if (Selected.Count > query.MaxItems) throw new InvalidOperationException("Context result exceeds query bound.");
            Fingerprint = RetrievalCanonical.Hash(new[]
            {
                RetrievalCanonical.Pair("source_contract", SourceContract),
                RetrievalCanonical.Pair("authority", Authority),
                RetrievalCanonical.Pair("owner_id", OwnerId),
                RetrievalCanonical.Pair("lineage_id", LineageId),
                RetrievalCanonical.Pair("counterpart_id", CounterpartId ?? "null"),
                RetrievalCanonical.Pair("purpose", Purpose.ToString().ToUpperInvariant()),
                RetrievalCanonical.Pair("query_fingerprint", QueryFingerprint),
                RetrievalCanonical.Pair("selected", string.Join(",", Selected.Select(value => value.Fingerprint))),
                RetrievalCanonical.Pair("metrics", string.Join(",", SelectionMetrics.Select(value => value.Key + "=" + value.Value.ToString(CultureInfo.InvariantCulture))))
            });
        }

        public string SourceContract { get; }
        public string Authority { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public DevelopmentalContextPurpose Purpose { get; }
        public string QueryFingerprint { get; }
        public IReadOnlyList<DevelopmentalContextItem> Selected { get; }
        public IReadOnlyDictionary<string, long> SelectionMetrics { get; }
        public string Fingerprint { get; }
    }

    internal static class RetrievalCanonical
    {
        internal static KeyValuePair<string, string> Pair(string key, string value) =>
            new KeyValuePair<string, string>(key, value);

        internal static string Text(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Canonical text is invalid.", name);
            return value;
        }

        internal static string? OptionalText(string? value, string name) =>
            value is null ? null : Text(value, name);

        internal static string Token(string value, string name)
        {
            value = Text(value, name);
            if (value.Any(character => !(char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.')))
                throw new ArgumentException("Canonical token is invalid.", name);
            return value.ToLowerInvariant();
        }

        internal static string Hex(string value, string name)
        {
            value = Text(value, name).ToLowerInvariant();
            if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
                throw new ArgumentException("Expected a 64-character SHA-256 value.", name);
            return value;
        }

        internal static IReadOnlyList<string> TextSet(IEnumerable<string> values, string name, int maximum)
        {
            var result = (values ?? throw new ArgumentNullException(name))
                .Select(value => Text(value, name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (result.Count == 0 || result.Count > maximum) throw new ArgumentOutOfRangeException(name);
            return new ReadOnlyCollection<string>(result);
        }

        internal static IReadOnlyList<string> TokenSet(IEnumerable<string> values, string name, int maximum)
        {
            var result = (values ?? throw new ArgumentNullException(name))
                .Select(value => Token(value, name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (result.Count > maximum) throw new ArgumentOutOfRangeException(name);
            return new ReadOnlyCollection<string>(result);
        }

        internal static IReadOnlyList<string> OrderedHexSet(IEnumerable<string> values, string name, int maximum)
        {
            var result = (values ?? throw new ArgumentNullException(name)).Select(value => Hex(value, name)).ToList();
            if (result.Count == 0 || result.Count > maximum || result.Count != result.Distinct(StringComparer.Ordinal).Count())
                throw new ArgumentOutOfRangeException(name);
            return new ReadOnlyCollection<string>(result);
        }

        internal static string Role(DevelopmentalContextRole role)
        {
            switch (role)
            {
                case DevelopmentalContextRole.DurableAnchor: return "DURABLE_ANCHOR";
                case DevelopmentalContextRole.RecentLived: return "RECENT_LIVED";
                case DevelopmentalContextRole.CategoryAnchor: return "CATEGORY_ANCHOR";
                case DevelopmentalContextRole.ContradictoryContext: return "CONTRADICTORY_CONTEXT";
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        internal static string Hash(IEnumerable<KeyValuePair<string, string>> fields)
        {
            var encoded = string.Join("\n", fields.Select(value =>
                value.Key.Length.ToString(CultureInfo.InvariantCulture) + ":" + value.Key + "=" +
                value.Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value.Value));
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(encoded))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
