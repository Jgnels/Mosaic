using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Dagmay.Core.Development
{
    public sealed class GroundedDevelopmentalContextRequest
    {
        public GroundedDevelopmentalContextRequest(
            DevelopmentalContextQuery retrievalQuery,
            DevelopmentalContextBundle bundle,
            IEnumerable<string> currentEventRootIds,
            long currentEventTick)
        {
            RetrievalQuery = retrievalQuery ?? throw new ArgumentNullException(nameof(retrievalQuery));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            CurrentEventRootIds = RetrievalCanonical.TextSet(
                currentEventRootIds ?? throw new ArgumentNullException(nameof(currentEventRootIds)),
                nameof(currentEventRootIds),
                8);
            if (currentEventTick < 0) throw new ArgumentOutOfRangeException(nameof(currentEventTick));
            CurrentEventTick = currentEventTick;
            ValidateBundle();
            if (Bundle.Selected.Any(value => value.EventTick > currentEventTick))
                throw new ArgumentException("Bundle contains future context evidence.", nameof(bundle));
            Fingerprint = GroundedJson.Hash(
                "{" +
                "\"bundle\":" + GroundedJson.String(Bundle.Fingerprint) + "," +
                "\"current_event_root_ids\":" + GroundedJson.Strings(CurrentEventRootIds) + "," +
                "\"current_event_tick\":" + GroundedJson.Number(CurrentEventTick) + "," +
                "\"retrieval_query\":" + GroundedJson.String(RetrievalQuery.Fingerprint) +
                "}");
        }

        public DevelopmentalContextQuery RetrievalQuery { get; }
        public DevelopmentalContextBundle Bundle { get; }
        public IReadOnlyList<string> CurrentEventRootIds { get; }
        public long CurrentEventTick { get; }
        public string Fingerprint { get; }

        private void ValidateBundle()
        {
            if (!string.Equals(Bundle.SourceContract, ContextualDevelopmentalRetrievalSource.Contract, StringComparison.Ordinal) ||
                !string.Equals(Bundle.Authority, ContextualDevelopmentalRetrievalSource.Authority, StringComparison.Ordinal) ||
                !string.Equals(Bundle.QueryFingerprint, RetrievalQuery.Fingerprint, StringComparison.Ordinal) ||
                !string.Equals(Bundle.OwnerId, RetrievalQuery.OwnerId, StringComparison.Ordinal) ||
                !string.Equals(Bundle.LineageId, RetrievalQuery.LineageId, StringComparison.Ordinal) ||
                !string.Equals(Bundle.CounterpartId, RetrievalQuery.CounterpartId, StringComparison.Ordinal) ||
                Bundle.Purpose != RetrievalQuery.Purpose ||
                Bundle.Selected.Count > GroundedDevelopmentalContextSource.MaximumItems)
                throw new ArgumentException("Bundle/query contract or identity mismatch.", nameof(Bundle));
            var expected = RetrievalCanonical.Hash(new[]
            {
                RetrievalCanonical.Pair("source_contract", Bundle.SourceContract),
                RetrievalCanonical.Pair("authority", Bundle.Authority),
                RetrievalCanonical.Pair("owner_id", Bundle.OwnerId),
                RetrievalCanonical.Pair("lineage_id", Bundle.LineageId),
                RetrievalCanonical.Pair("counterpart_id", Bundle.CounterpartId ?? "null"),
                RetrievalCanonical.Pair("purpose", Bundle.Purpose.ToString().ToUpperInvariant()),
                RetrievalCanonical.Pair("query_fingerprint", Bundle.QueryFingerprint),
                RetrievalCanonical.Pair("selected", string.Join(",", Bundle.Selected.Select(value => value.Fingerprint))),
                RetrievalCanonical.Pair("metrics", string.Join(",", Bundle.SelectionMetrics.Select(value =>
                    value.Key + "=" + value.Value.ToString(CultureInfo.InvariantCulture))))
            });
            if (!string.Equals(expected, Bundle.Fingerprint, StringComparison.Ordinal))
                throw new ArgumentException("Forged v41 bundle fingerprint.", nameof(Bundle));
        }
    }

    public sealed class GroundedDevelopmentalContextMaterializer
    {
        private readonly TrustedContextProjectionRegistry _registry;

        public GroundedDevelopmentalContextMaterializer(TrustedContextProjectionRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public GroundedDevelopmentalContextPacket Materialize(
            GroundedDevelopmentalContextRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var query = request.RetrievalQuery;
            if (!string.Equals(query.SaveId, _registry.SaveId, StringComparison.Ordinal) ||
                !string.Equals(query.WorldId, _registry.WorldId, StringComparison.Ordinal) ||
                !string.Equals(query.StoreSetId, _registry.StoreSetId, StringComparison.Ordinal))
                throw new InvalidOperationException("Foreign materialization store binding.");

            var versionBefore = _registry.MutationVersion;
            var currentRoots = new HashSet<string>(request.CurrentEventRootIds, StringComparer.Ordinal);
            var usedRoots = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<GroundedDevelopmentalContextItem>();
            var contributions = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var directionalItems = 0;
            var memoryItems = 0;
            var unusedCap = 0;

            foreach (var selected in request.Bundle.Selected)
            {
                var identity = GroundedTokens.Kind(selected.Kind) + "\n" + selected.EvidenceId;
                if (!seen.Add(identity))
                    throw new InvalidOperationException("Duplicate selected evidence identity.");
                var projection = Resolve(selected, query, currentRoots, usedRoots);
                foreach (var root in projection.RootEventIds) usedRoots.Add(root);
                var allocated = AllocateRoleBudget(projection.Dimensions, selected.ContributionCapBps);
                if (allocated.Count > 0)
                {
                    directionalItems++;
                    foreach (var value in allocated)
                    {
                        if (!contributions.TryGetValue(value.Dimension, out var values))
                        {
                            values = new List<int>();
                            contributions.Add(value.Dimension, values);
                        }
                        values.Add(value.ValueBps);
                    }
                }
                else
                {
                    unusedCap += selected.ContributionCapBps;
                    if (selected.Kind == DevelopmentalEvidenceKind.Memory) memoryItems++;
                }
                items.Add(new GroundedDevelopmentalContextItem(
                    selected.Role,
                    selected.Kind,
                    selected.EvidenceId,
                    selected.SourceEventId,
                    selected.RootEventIds,
                    selected.EventTick,
                    selected.Category,
                    selected.ReasonCodes,
                    selected.ContributionCapBps,
                    allocated,
                    projection.Salience,
                    projection.Durability,
                    projection.Fingerprint));
            }

            var totalAllocated = items.Sum(item =>
                item.AllocatedDimensionsBps.Sum(value => Math.Abs(value.ValueBps)));
            var totalCaps = items.Sum(item => item.ContributionCapBps);
            if (items.Count > GroundedDevelopmentalContextSource.MaximumItems ||
                totalAllocated > totalCaps || totalCaps > 10_000)
                throw new InvalidOperationException("Materialization influence bound exceeded.");

            var syntheses = new List<GroundedDimensionSynthesis>();
            var contradictionDimensions = 0;
            foreach (var pair in contributions.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                var positive = pair.Value.Where(value => value > 0).Sum();
                var negative = -pair.Value.Where(value => value < 0).Sum();
                var net = positive - negative;
                var positiveCount = pair.Value.Count(value => value > 0);
                var negativeCount = pair.Value.Count(value => value < 0);
                var contradictionCount = Math.Min(positiveCount, negativeCount);
                if (positive > 0 && negative > 0) contradictionDimensions++;
                var total = positive + negative;
                var confidence = total == 0 ? 0 : (int)((long)Math.Abs(net) * 10_000L / total);
                syntheses.Add(new GroundedDimensionSynthesis(
                    pair.Key, net, positive, negative, pair.Value.Count,
                    contradictionCount, confidence));
            }

            var uncertainty = new List<string>();
            if (items.Count == 0) uncertainty.Add("NO_ELIGIBLE_HISTORICAL_CONTEXT");
            if (memoryItems > 0) uncertainty.Add("MEMORY_CONTEXT_HAS_NO_INVENTED_DIRECTION");
            if (contradictionDimensions > 0) uncertainty.Add("CONTRADICTORY_HISTORY_PRESERVED");
            if (directionalItems == 0 && items.Count > 0)
                uncertainty.Add("NO_DIRECTIONAL_DURABLE_EVIDENCE");
            if (unusedCap > 0) uncertainty.Add("UNUSED_ROLE_CAP_NOT_RENORMALIZED");

            var metrics = new[]
            {
                Metric("selected_items", items.Count),
                Metric("directional_items", directionalItems),
                Metric("memory_items", memoryItems),
                Metric("dimension_count", syntheses.Count),
                Metric("contradiction_dimensions", contradictionDimensions),
                Metric("allocated_influence_bps", totalAllocated),
                Metric("declared_role_caps_bps", totalCaps),
                Metric("unused_role_cap_bps", unusedCap),
                Metric("full_canonical_objects_retained", 0)
            };
            var packet = new GroundedDevelopmentalContextPacket(
                query.OwnerId,
                query.LineageId,
                query.CounterpartId,
                query.Purpose,
                request.Fingerprint,
                request.Bundle.Fingerprint,
                items,
                syntheses,
                uncertainty,
                metrics);
            if (versionBefore != _registry.MutationVersion)
                throw new InvalidOperationException("Materialization mutated its projection registry.");
            return GroundedDevelopmentalContextPacket.RequireValid(packet);
        }

        private ContextEvidenceProjection Resolve(
            DevelopmentalContextItem selected,
            DevelopmentalContextQuery query,
            ISet<string> currentRoots,
            ISet<string> usedRoots)
        {
            var projection = _registry.Resolve(selected.Kind, selected.EvidenceId);
            if (!string.Equals(projection.OwnerId, query.OwnerId, StringComparison.Ordinal) ||
                !string.Equals(projection.LineageId, query.LineageId, StringComparison.Ordinal))
                throw new InvalidOperationException("Foreign owner or lineage projection.");
            if (query.CounterpartId is not null &&
                (selected.Role == DevelopmentalContextRole.DurableAnchor ||
                 selected.Role == DevelopmentalContextRole.ContradictoryContext) &&
                !string.Equals(projection.CounterpartId, query.CounterpartId, StringComparison.Ordinal))
                throw new InvalidOperationException("Foreign counterpart projection.");
            if (projection.EventTick > query.CurrentTick ||
                projection.CheckpointGeneration > query.CheckpointGeneration)
                throw new InvalidOperationException("Future projection.");
            if (!query.CheckpointAncestry.Contains(projection.CheckpointFingerprint, StringComparer.Ordinal))
                throw new InvalidOperationException("Rollback-orphaned or substituted checkpoint.");
            if (projection.StoreDigest is not null &&
                !string.Equals(projection.StoreDigest, _registry.StoreDigest, StringComparison.Ordinal))
                throw new InvalidOperationException("Foreign canonical store projection.");
            if (!string.Equals(selected.SourceEventId, projection.SourceEventId, StringComparison.Ordinal) ||
                !selected.RootEventIds.SequenceEqual(projection.RootEventIds) ||
                selected.EventTick != projection.EventTick ||
                !string.Equals(selected.Category, projection.Category, StringComparison.Ordinal))
                throw new InvalidOperationException("Selected projection provenance mismatch.");
            if (projection.RootEventIds.Any(currentRoots.Contains))
                throw new InvalidOperationException("Current event cannot be reused as historical context.");
            if (projection.RootEventIds.Any(usedRoots.Contains))
                throw new InvalidOperationException("Duplicate historical root across selected roles.");
            return projection;
        }

        internal static IReadOnlyList<GroundedDimensionContribution> AllocateRoleBudget(
            IEnumerable<KeyValuePair<string, int>> dimensions,
            int capBps)
        {
            var values = (dimensions ?? throw new ArgumentNullException(nameof(dimensions)))
                .Where(value => value.Value != 0)
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToList();
            if (values.Count == 0 || capBps <= 0)
                return new ReadOnlyCollection<GroundedDimensionContribution>(
                    new List<GroundedDimensionContribution>());
            var total = values.Sum(value => (long)Math.Abs(value.Value));
            var allocations = new List<Allocation>();
            var used = 0;
            foreach (var value in values)
            {
                var numerator = capBps * (long)Math.Abs(value.Value);
                var amount = (int)(numerator / total);
                allocations.Add(new Allocation(
                    value.Key,
                    value.Value > 0 ? amount : -amount,
                    numerator % total));
                used += amount;
            }
            var remaining = capBps - used;
            foreach (var allocation in allocations
                .OrderByDescending(value => value.Remainder)
                .ThenByDescending(value => value.Dimension, StringComparer.Ordinal)
                .Take(remaining))
                allocation.Value += allocation.Value >= 0 ? 1 : -1;
            return new ReadOnlyCollection<GroundedDimensionContribution>(
                allocations.Where(value => value.Value != 0)
                    .OrderBy(value => value.Dimension, StringComparer.Ordinal)
                    .Select(value => new GroundedDimensionContribution(value.Dimension, value.Value))
                    .ToList());
        }

        private static KeyValuePair<string, long> Metric(string key, long value) =>
            new KeyValuePair<string, long>(key, value);

        private sealed class Allocation
        {
            internal Allocation(string dimension, int value, long remainder)
            {
                Dimension = dimension;
                Value = value;
                Remainder = remainder;
            }
            internal string Dimension { get; }
            internal int Value { get; set; }
            internal long Remainder { get; }
        }
    }
}
