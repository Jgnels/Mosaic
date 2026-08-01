using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Development
{
    public sealed class ContextualDevelopmentalRetrievalIndex
    {
        private readonly string _saveId;
        private readonly string _worldId;
        private readonly string _storeSetId;
        private readonly string _storeDigest;
        private readonly Dictionary<string, IReadOnlyList<Candidate>> _pair =
            new Dictionary<string, IReadOnlyList<Candidate>>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Candidate>> _category =
            new Dictionary<string, IReadOnlyList<Candidate>>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Candidate>> _recent =
            new Dictionary<string, IReadOnlyList<Candidate>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _evidenceIdentity =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public ContextualDevelopmentalRetrievalIndex(
            DurableCanonicalStoreSet stores,
            IEventLedger eventLedger,
            IEnumerable<CheckpointCommitReceipt> checkpointReceipts,
            IEnumerable<ContextualMemoryEvidence> memories)
        {
            if (stores is null) throw new ArgumentNullException(nameof(stores));
            if (eventLedger is null) throw new ArgumentNullException(nameof(eventLedger));
            if (!stores.Healthy) throw new InvalidOperationException("Canonical store set is unhealthy.");
            _saveId = stores.SaveId;
            _worldId = stores.WorldId;
            _storeSetId = stores.StoreSetId;
            _storeDigest = stores.StateDigest();

            var receipts = ValidateReceipts(checkpointReceipts);
            var events = eventLedger.Snapshot()
                .GroupBy(value => value.Value.Id.ToString(), StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.Select(item => item.Value).ToList(), StringComparer.Ordinal);

            foreach (var record in stores.DevelopmentalRecords)
            {
                if (!receipts.TryGetValue(record.CheckpointGeneration, out var receipt)) continue;
                var candidate = TrustedDevelopmentalCandidate(record, receipt, events);
                Add(candidate);
            }

            foreach (var memory in memories ?? throw new ArgumentNullException(nameof(memories)))
                Add(MemoryCandidate(memory));

            StateFingerprint = RetrievalCanonical.Hash(new[]
            {
                RetrievalCanonical.Pair("schema", ContextualDevelopmentalRetrievalSource.Contract),
                RetrievalCanonical.Pair("save_id", _saveId),
                RetrievalCanonical.Pair("world_id", _worldId),
                RetrievalCanonical.Pair("store_set_id", _storeSetId),
                RetrievalCanonical.Pair("store_digest", _storeDigest),
                RetrievalCanonical.Pair("candidates", string.Join(",", AllCandidates()
                    .OrderBy(value => value.Fingerprint, StringComparer.Ordinal)
                    .Select(value => value.Fingerprint)))
            });
        }

        public string StateFingerprint { get; }
        public int MaximumObservedCandidatesPerKey =>
            _pair.Values.Concat(_category.Values).Concat(_recent.Values)
                .Select(value => value.Count).DefaultIfEmpty(0).Max();

        public DevelopmentalContextBundle Retrieve(DevelopmentalContextQuery query)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            if (!string.Equals(query.SaveId, _saveId, StringComparison.Ordinal) ||
                !string.Equals(query.WorldId, _worldId, StringComparison.Ordinal) ||
                !string.Equals(query.StoreSetId, _storeSetId, StringComparison.Ordinal))
                throw new InvalidOperationException("Query is bound to a foreign canonical store set.");

            var pairCandidates = PairCandidates(query);
            var recentCandidates = Window(_recent, OwnerKey(query.OwnerId, query.LineageId));
            var categoryCandidates = query.ContextTags
                .SelectMany(tag => Window(_category, CategoryKey(query.OwnerId, query.LineageId, tag)))
                .Distinct(CandidateFingerprintComparer.Instance)
                .ToList();
            var ancestry = new HashSet<string>(query.CheckpointAncestry, StringComparer.Ordinal);
            pairCandidates = Eligible(pairCandidates, query, ancestry).ToList();
            recentCandidates = Eligible(recentCandidates, query, ancestry).ToList();
            categoryCandidates = Eligible(categoryCandidates, query, ancestry).ToList();

            var selected = new List<DevelopmentalContextItem>();
            var usedRoots = new HashSet<string>(StringComparer.Ordinal);
            var duplicateRootSkips = 0L;

            var durable = RankDurable(pairCandidates.Where(value =>
                value.Kind == DevelopmentalEvidenceKind.Developmental && value.EffectMagnitude > 0), query).FirstOrDefault();
            Select(durable, DevelopmentalContextRole.DurableAnchor, 4000,
                new[] { "DURABLE_CANONICAL_SUCCESS", "EXACT_COUNTERPART" });

            var recent = RankRecent(recentCandidates.Where(value => value.Kind == DevelopmentalEvidenceKind.Memory), query).FirstOrDefault(value => DistinctRoots(value));
            Select(recent, DevelopmentalContextRole.RecentLived, 3000,
                new[] { "FROZEN_MEMORY_CATEGORY_PRESERVED", "OWNER_EXPERIENCED", "RECENT_SALIENT" });

            var category = RankCategory(categoryCandidates, query).FirstOrDefault(value =>
                DistinctRoots(value) && selected.All(item => !string.Equals(item.Category, value.Category, StringComparison.Ordinal)));
            Select(category, DevelopmentalContextRole.CategoryAnchor, 2000,
                new[] { "FROZEN_MEMORY_CATEGORY_PRESERVED" });

            var contradiction = durable is null ? null : RankContradiction(
                    pairCandidates.Concat(recentCandidates).Distinct(CandidateFingerprintComparer.Instance)
                        .Where(value => value.Polarity != 0 && value.Polarity == -durable.Polarity),
                    query)
                .FirstOrDefault(value => DistinctRoots(value));
            Select(contradiction, DevelopmentalContextRole.ContradictoryContext, 1000,
                new[] { "CONTRADICTORY_CONTEXT_PRESERVED" });

            if (selected.Count > query.MaxItems) selected = selected.Take(query.MaxItems).ToList();
            return new DevelopmentalContextBundle(query, selected, new[]
            {
                Metric("candidate_window_per_key", ContextualDevelopmentalRetrievalSource.MaximumCandidatesPerKey),
                Metric("duplicate_root_skips", duplicateRootSkips),
                Metric("examined_category_candidates", categoryCandidates.Count),
                Metric("examined_pair_candidates", pairCandidates.Count),
                Metric("examined_recent_candidates", recentCandidates.Count),
                Metric("selected_count", selected.Count)
            });

            bool DistinctRoots(Candidate candidate)
            {
                var distinct = candidate.RootEventIds.All(value => !usedRoots.Contains(value));
                if (!distinct) duplicateRootSkips++;
                return distinct;
            }

            void Select(Candidate? candidate, DevelopmentalContextRole role, int cap, IEnumerable<string> reasons)
            {
                if (candidate is null || selected.Count >= query.MaxItems || !DistinctRoots(candidate)) return;
                foreach (var root in candidate.RootEventIds) usedRoots.Add(root);
                selected.Add(new DevelopmentalContextItem(
                    candidate.EvidenceId, candidate.Kind, role, candidate.Category,
                    candidate.SourceEventId, candidate.RootEventIds, candidate.EventTick, cap, reasons));
            }
        }

        private Dictionary<long, CheckpointCommitReceipt> ValidateReceipts(
            IEnumerable<CheckpointCommitReceipt> checkpointReceipts)
        {
            var result = new Dictionary<long, CheckpointCommitReceipt>();
            foreach (var receipt in checkpointReceipts ?? throw new ArgumentNullException(nameof(checkpointReceipts)))
            {
                if (receipt is null ||
                    !string.Equals(receipt.Fingerprint, receipt.ComputeFingerprint(), StringComparison.Ordinal) ||
                    !receipt.Completed || !receipt.Writable || !receipt.StoresHealthy ||
                    !string.Equals(receipt.SaveId, _saveId, StringComparison.Ordinal) ||
                    !string.Equals(receipt.WorldId, _worldId, StringComparison.Ordinal) ||
                    !string.Equals(receipt.StoreSetId, _storeSetId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Checkpoint receipt is not trusted for this store set.");
                if (result.TryGetValue(receipt.CheckpointGeneration, out var existing) &&
                    !string.Equals(existing.Fingerprint, receipt.Fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("Conflicting checkpoint receipts share a generation.");
                result[receipt.CheckpointGeneration] = receipt;
            }
            return result;
        }

        private static Candidate TrustedDevelopmentalCandidate(
            DevelopmentalAppraisalRecord record,
            CheckpointCommitReceipt receipt,
            IReadOnlyDictionary<string, List<EnvironmentEvent>> events)
        {
            if (!events.TryGetValue(record.SourceEventId, out var matches) || matches.Count != 1)
                throw new InvalidOperationException("Developmental source event is missing or duplicated.");
            var sourceEvent = matches[0];
            var eventHash = new DialogueAdmissionOutboxCodec().ComputeEventHash(sourceEvent);
            if (!string.Equals(eventHash, record.SourceEventHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Developmental source event hash mismatch.");
            if (!sourceEvent.GameTick.HasValue || sourceEvent.GameTick.Value < 0)
                throw new InvalidOperationException("Developmental source event lacks a canonical tick.");
            var subjectIds = new HashSet<string>(sourceEvent.Subjects.Select(value => value.ToString()), StringComparer.Ordinal);
            if (!subjectIds.Contains(record.OwnerId) || !subjectIds.Contains(record.CounterpartId))
                throw new InvalidOperationException("Developmental source event subjects do not match owner and counterpart.");
            if (!string.Equals(Payload(sourceEvent, "mosaic_admitted"), "true", StringComparison.Ordinal) ||
                !string.Equals(Payload(sourceEvent, "mosaic_privacy"), "OWNER_PRIVATE", StringComparison.Ordinal) ||
                !string.Equals(Payload(sourceEvent, "mosaic_lineage_id"), record.LineageId, StringComparison.Ordinal))
                throw new InvalidOperationException("Developmental source event admission, privacy, or lineage mismatch.");

            var roots = PayloadList(sourceEvent, "mosaic_root_event_ids", new[] { record.SourceEventId }, 8, false);
            if (roots.Any(value => value.StartsWith("developmental-record:", StringComparison.Ordinal)))
                throw new InvalidOperationException("Recursive developmental roots are forbidden.");
            var tags = PayloadList(sourceEvent, "mosaic_context_tags", EventTags(sourceEvent), 16, true);
            var category = Payload(sourceEvent, "mosaic_category") ?? Category(record);
            var effect = Magnitude(record);
            return new Candidate(
                record.RecordId, DevelopmentalEvidenceKind.Developmental, record.OwnerId, record.LineageId,
                record.CounterpartId, record.SourceEventId, roots, sourceEvent.GameTick.Value,
                RetrievalCanonical.Token(category, nameof(category)), tags, Math.Min(100, effect / 1000),
                effect, Polarity(record), record.CheckpointGeneration, receipt.CheckpointHash,
                record.Fingerprint());
        }

        private static Candidate MemoryCandidate(ContextualMemoryEvidence memory) =>
            new Candidate(
                memory.MemoryId, DevelopmentalEvidenceKind.Memory, memory.OwnerId, memory.LineageId,
                memory.CounterpartId, memory.SourceEventId, memory.RootEventIds, memory.ObservedTick,
                memory.Category, memory.Tags, memory.Salience, memory.Salience * 1000, 0,
                memory.CheckpointGeneration, memory.CheckpointFingerprint, memory.Fingerprint);

        private void Add(Candidate candidate)
        {
            if (_evidenceIdentity.TryGetValue(candidate.EvidenceId, out var existingFingerprint))
            {
                if (!string.Equals(existingFingerprint, candidate.Fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("Conflicting evidence identity.");
                return;
            }
            _evidenceIdentity.Add(candidate.EvidenceId, candidate.Fingerprint);
            AddWindow(_recent, OwnerKey(candidate.OwnerId, candidate.LineageId), candidate);
            AddWindow(_category, CategoryKey(candidate.OwnerId, candidate.LineageId, candidate.Category), candidate);
            if (candidate.CounterpartId is not null)
                AddWindow(_pair, PairKey(candidate.OwnerId, candidate.LineageId, candidate.CounterpartId), candidate);
        }

        private static void AddWindow(
            IDictionary<string, IReadOnlyList<Candidate>> index,
            string key,
            Candidate candidate)
        {
            var values = index.TryGetValue(key, out var existing) ? existing.ToList() : new List<Candidate>();
            values.RemoveAll(value => string.Equals(value.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal));
            values.Add(candidate);
            index[key] = new ReadOnlyCollection<Candidate>(values
                .OrderByDescending(value => value.EventTick)
                .ThenByDescending(value => value.Salience)
                .ThenBy(value => value.EvidenceId, StringComparer.Ordinal)
                .Take(ContextualDevelopmentalRetrievalSource.MaximumCandidatesPerKey)
                .ToList());
        }

        private IEnumerable<Candidate> AllCandidates() =>
            _recent.Values.SelectMany(value => value).Distinct(CandidateFingerprintComparer.Instance);

        private List<Candidate> PairCandidates(DevelopmentalContextQuery query) =>
            query.CounterpartId is null
                ? Window(_recent, OwnerKey(query.OwnerId, query.LineageId))
                : Window(_pair, PairKey(query.OwnerId, query.LineageId, query.CounterpartId));

        private static IEnumerable<Candidate> Eligible(
            IEnumerable<Candidate> candidates,
            DevelopmentalContextQuery query,
            ISet<string> ancestry) =>
            candidates.Where(value =>
                string.Equals(value.OwnerId, query.OwnerId, StringComparison.Ordinal) &&
                string.Equals(value.LineageId, query.LineageId, StringComparison.Ordinal) &&
                value.CheckpointGeneration <= query.CheckpointGeneration &&
                ancestry.Contains(value.CheckpointFingerprint) &&
                value.EventTick <= query.CurrentTick &&
                (query.CounterpartId is null || value.CounterpartId is null ||
                 string.Equals(value.CounterpartId, query.CounterpartId, StringComparison.Ordinal)));

        private static IOrderedEnumerable<Candidate> RankDurable(IEnumerable<Candidate> values, DevelopmentalContextQuery query) =>
            values.OrderByDescending(value => Overlap(value, query))
                .ThenByDescending(value => value.EffectMagnitude)
                .ThenByDescending(value => value.Salience)
                .ThenByDescending(value => value.EventTick)
                .ThenBy(value => value.EvidenceId, StringComparer.Ordinal);

        private static IOrderedEnumerable<Candidate> RankRecent(IEnumerable<Candidate> values, DevelopmentalContextQuery query) =>
            values.OrderByDescending(value => Overlap(value, query))
                .ThenByDescending(value => value.EventTick)
                .ThenByDescending(value => value.Salience)
                .ThenBy(value => value.Kind == DevelopmentalEvidenceKind.Memory ? 0 : 1)
                .ThenBy(value => value.EvidenceId, StringComparer.Ordinal);

        private static IOrderedEnumerable<Candidate> RankCategory(IEnumerable<Candidate> values, DevelopmentalContextQuery query) =>
            values.OrderByDescending(value => Overlap(value, query))
                .ThenByDescending(value => value.Salience)
                .ThenByDescending(value => value.EventTick)
                .ThenBy(value => value.EvidenceId, StringComparer.Ordinal);

        private static IOrderedEnumerable<Candidate> RankContradiction(IEnumerable<Candidate> values, DevelopmentalContextQuery query) =>
            values.OrderByDescending(value => Overlap(value, query))
                .ThenByDescending(value => value.Salience)
                .ThenByDescending(value => value.EventTick)
                .ThenBy(value => value.EvidenceId, StringComparer.Ordinal);

        private static int Overlap(Candidate candidate, DevelopmentalContextQuery query) =>
            candidate.Tags.Intersect(query.ContextTags, StringComparer.Ordinal).Count();

        private static List<Candidate> Window(
            IReadOnlyDictionary<string, IReadOnlyList<Candidate>> index,
            string key) =>
            index.TryGetValue(key, out var values) ? values.ToList() : new List<Candidate>();

        private static string OwnerKey(string owner, string lineage) => owner + "\n" + lineage;
        private static string PairKey(string owner, string lineage, string counterpart) =>
            OwnerKey(owner, lineage) + "\n" + counterpart;
        private static string CategoryKey(string owner, string lineage, string category) =>
            OwnerKey(owner, lineage) + "\n" + category;

        private static KeyValuePair<string, long> Metric(string key, long value) =>
            new KeyValuePair<string, long>(key, value);

        private static string? Payload(EnvironmentEvent sourceEvent, string key) =>
            sourceEvent.FactualPayload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        private static IReadOnlyList<string> PayloadList(
            EnvironmentEvent sourceEvent,
            string key,
            IEnumerable<string> fallback,
            int maximum,
            bool tokens)
        {
            var values = Payload(sourceEvent, key)?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? fallback.ToArray();
            return tokens
                ? RetrievalCanonical.TokenSet(values, key, maximum)
                : RetrievalCanonical.TextSet(values, key, maximum);
        }

        private static IEnumerable<string> EventTags(EnvironmentEvent sourceEvent)
        {
            var tags = sourceEvent.Kind.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => value.Length > 2)
                .Select(value => value.ToLowerInvariant())
                .ToList();
            if (tags.Count == 0) tags.Add("event");
            return tags;
        }

        private static string Category(DevelopmentalAppraisalRecord record)
        {
            var affect = AffectMagnitude(record) > 0;
            var relationship = RelationshipMagnitude(record) > 0;
            if (affect && relationship) return "mixed-social";
            return relationship ? "relationship" : "affect";
        }

        private static int Magnitude(DevelopmentalAppraisalRecord record) =>
            AffectMagnitude(record) + RelationshipMagnitude(record);

        private static int AffectMagnitude(DevelopmentalAppraisalRecord record) =>
            DecimalMagnitude(
                record.ActualAffectDelta.Valence, record.ActualAffectDelta.Arousal,
                record.ActualAffectDelta.Threat, record.ActualAffectDelta.Agency,
                record.ActualAffectDelta.Attachment, record.ActualAffectDelta.Certainty,
                record.ActualAffectDelta.SocialStanding);

        private static int RelationshipMagnitude(DevelopmentalAppraisalRecord record) =>
            DecimalMagnitude(
                record.ActualRelationshipDelta.Trust, record.ActualRelationshipDelta.Affection,
                record.ActualRelationshipDelta.Resentment, record.ActualRelationshipDelta.Fear);

        private static int DecimalMagnitude(params decimal[] values) =>
            (int)Math.Min(int.MaxValue, values.Sum(value => Math.Abs(value)) * 1_000_000m);

        private static int Polarity(DevelopmentalAppraisalRecord record)
        {
            var score = record.ActualAffectDelta.Valence + record.ActualAffectDelta.Agency +
                        record.ActualRelationshipDelta.Trust + record.ActualRelationshipDelta.Affection +
                        -record.ActualAffectDelta.Threat -
                        record.ActualRelationshipDelta.Resentment - record.ActualRelationshipDelta.Fear;
            return score == 0m ? 0 : score > 0m ? 1 : -1;
        }

        private sealed class Candidate
        {
            internal Candidate(
                string evidenceId,
                DevelopmentalEvidenceKind kind,
                string ownerId,
                string lineageId,
                string? counterpartId,
                string sourceEventId,
                IEnumerable<string> rootEventIds,
                long eventTick,
                string category,
                IEnumerable<string> tags,
                int salience,
                int effectMagnitude,
                int polarity,
                long checkpointGeneration,
                string checkpointFingerprint,
                string sourceFingerprint)
            {
                EvidenceId = evidenceId;
                Kind = kind;
                OwnerId = ownerId;
                LineageId = lineageId;
                CounterpartId = counterpartId;
                SourceEventId = sourceEventId;
                RootEventIds = new ReadOnlyCollection<string>(rootEventIds.OrderBy(value => value, StringComparer.Ordinal).ToList());
                EventTick = eventTick;
                Category = category;
                Tags = new ReadOnlyCollection<string>(tags.OrderBy(value => value, StringComparer.Ordinal).ToList());
                Salience = salience;
                EffectMagnitude = effectMagnitude;
                Polarity = polarity;
                CheckpointGeneration = checkpointGeneration;
                CheckpointFingerprint = checkpointFingerprint;
                Fingerprint = RetrievalCanonical.Hash(new[]
                {
                    RetrievalCanonical.Pair("source_fingerprint", sourceFingerprint),
                    RetrievalCanonical.Pair("checkpoint_fingerprint", checkpointFingerprint),
                    RetrievalCanonical.Pair("category", category),
                    RetrievalCanonical.Pair("tags", string.Join(",", Tags)),
                    RetrievalCanonical.Pair("roots", string.Join(",", RootEventIds))
                });
            }

            internal string EvidenceId { get; }
            internal DevelopmentalEvidenceKind Kind { get; }
            internal string OwnerId { get; }
            internal string LineageId { get; }
            internal string? CounterpartId { get; }
            internal string SourceEventId { get; }
            internal IReadOnlyList<string> RootEventIds { get; }
            internal long EventTick { get; }
            internal string Category { get; }
            internal IReadOnlyList<string> Tags { get; }
            internal int Salience { get; }
            internal int EffectMagnitude { get; }
            internal int Polarity { get; }
            internal long CheckpointGeneration { get; }
            internal string CheckpointFingerprint { get; }
            internal string Fingerprint { get; }
        }

        private sealed class CandidateFingerprintComparer : IEqualityComparer<Candidate>
        {
            internal static readonly CandidateFingerprintComparer Instance = new CandidateFingerprintComparer();
            public bool Equals(Candidate? left, Candidate? right) =>
                ReferenceEquals(left, right) || (left is not null && right is not null &&
                string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal));
            public int GetHashCode(Candidate value) => StringComparer.Ordinal.GetHashCode(value.Fingerprint);
        }
    }
}
