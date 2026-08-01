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
    internal sealed class ContextEvidenceProjection
    {
        internal ContextEvidenceProjection(
            DevelopmentalEvidenceKind kind,
            string evidenceId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string sourceEventId,
            IEnumerable<string> rootEventIds,
            long eventTick,
            string category,
            IEnumerable<string> tags,
            IEnumerable<KeyValuePair<string, int>> dimensions,
            int salience,
            int durability,
            long checkpointGeneration,
            string checkpointFingerprint,
            string? storeDigest,
            string sourceContract,
            string sourceGateDigest,
            string sourceFingerprint)
        {
            Kind = kind;
            EvidenceId = RetrievalCanonical.Text(evidenceId, nameof(evidenceId));
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            SourceEventId = RetrievalCanonical.Text(sourceEventId, nameof(sourceEventId));
            RootEventIds = RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8);
            if (RootEventIds.Any(value =>
                value.StartsWith("developmental-record:", StringComparison.Ordinal) ||
                value.StartsWith("context-packet:", StringComparison.Ordinal)))
                throw new ArgumentException("Recursive derived provenance is forbidden.", nameof(rootEventIds));
            if (eventTick < 0 || checkpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(eventTick));
            EventTick = eventTick;
            Category = RetrievalCanonical.Token(category, nameof(category));
            Tags = RetrievalCanonical.TokenSet(tags, nameof(tags), 16);
            Dimensions = new ReadOnlyCollection<KeyValuePair<string, int>>(
                (dimensions ?? throw new ArgumentNullException(nameof(dimensions)))
                    .Select(value => new KeyValuePair<string, int>(
                        RetrievalCanonical.Token(value.Key, nameof(dimensions)), value.Value))
                    .Where(value => value.Value != 0)
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToList());
            if (Dimensions.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != Dimensions.Count ||
                Dimensions.Any(value => value.Value < -GroundedDevelopmentalContextSource.MaximumCanonicalDelta ||
                                        value.Value > GroundedDevelopmentalContextSource.MaximumCanonicalDelta))
                throw new ArgumentException("Projection dimensions are invalid.", nameof(dimensions));
            if (salience < 0 || salience > 100 || durability < 0 || durability > 100)
                throw new ArgumentOutOfRangeException(nameof(salience));
            Salience = salience;
            Durability = durability;
            CheckpointGeneration = checkpointGeneration;
            CheckpointFingerprint = RetrievalCanonical.Hex(checkpointFingerprint, nameof(checkpointFingerprint));
            StoreDigest = storeDigest is null ? null : RetrievalCanonical.Hex(storeDigest, nameof(storeDigest));
            SourceContract = RetrievalCanonical.Text(sourceContract, nameof(sourceContract));
            SourceGateDigest = RetrievalCanonical.Hex(sourceGateDigest, nameof(sourceGateDigest));
            SourceFingerprint = RetrievalCanonical.Hex(sourceFingerprint, nameof(sourceFingerprint));
            Fingerprint = GroundedJson.Hash(DeterministicJson());
        }

        internal DevelopmentalEvidenceKind Kind { get; }
        internal string EvidenceId { get; }
        internal string OwnerId { get; }
        internal string LineageId { get; }
        internal string? CounterpartId { get; }
        internal string SourceEventId { get; }
        internal IReadOnlyList<string> RootEventIds { get; }
        internal long EventTick { get; }
        internal string Category { get; }
        internal IReadOnlyList<string> Tags { get; }
        internal IReadOnlyList<KeyValuePair<string, int>> Dimensions { get; }
        internal int Salience { get; }
        internal int Durability { get; }
        internal long CheckpointGeneration { get; }
        internal string CheckpointFingerprint { get; }
        internal string? StoreDigest { get; }
        internal string SourceContract { get; }
        internal string SourceGateDigest { get; }
        internal string SourceFingerprint { get; }
        internal string Fingerprint { get; }

        private string DeterministicJson() =>
            "{" +
            "\"category\":" + GroundedJson.String(Category) + "," +
            "\"checkpoint_fingerprint\":" + GroundedJson.String(CheckpointFingerprint) + "," +
            "\"checkpoint_generation\":" + GroundedJson.Number(CheckpointGeneration) + "," +
            "\"counterpart_id\":" + GroundedJson.OptionalString(CounterpartId) + "," +
            "\"dimensions\":" + GroundedJson.IntPairs(Dimensions) + "," +
            "\"durability\":" + GroundedJson.Number(Durability) + "," +
            "\"event_tick\":" + GroundedJson.Number(EventTick) + "," +
            "\"evidence_id\":" + GroundedJson.String(EvidenceId) + "," +
            "\"kind\":" + GroundedJson.String(GroundedTokens.Kind(Kind)) + "," +
            "\"lineage_id\":" + GroundedJson.String(LineageId) + "," +
            "\"owner_id\":" + GroundedJson.String(OwnerId) + "," +
            "\"privacy\":\"OWNER_PRIVATE\"," +
            "\"root_event_ids\":" + GroundedJson.Strings(RootEventIds) + "," +
            "\"salience\":" + GroundedJson.Number(Salience) + "," +
            "\"source_contract\":" + GroundedJson.String(SourceContract) + "," +
            "\"source_event_id\":" + GroundedJson.String(SourceEventId) + "," +
            "\"source_fingerprint\":" + GroundedJson.String(SourceFingerprint) + "," +
            "\"source_gate_digest\":" + GroundedJson.String(SourceGateDigest) + "," +
            "\"store_digest\":" + GroundedJson.OptionalString(StoreDigest) + "," +
            "\"tags\":" + GroundedJson.Strings(Tags) +
            "}";
    }

    public sealed class TrustedContextProjectionRegistry
    {
        private readonly string _saveId;
        private readonly string _worldId;
        private readonly string _storeSetId;
        private readonly string _storeDigest;
        private readonly Dictionary<string, ContextEvidenceProjection> _byId =
            new Dictionary<string, ContextEvidenceProjection>(StringComparer.Ordinal);
        private readonly byte[] _fingerprintSum = new byte[32];
        private readonly byte[] _fingerprintXor = new byte[32];
        private long _mutationVersion;
        private string? _cachedStateFingerprint;

        public TrustedContextProjectionRegistry(
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
            foreach (var record in stores.DevelopmentalRecords.OrderBy(value => value.RecordId, StringComparer.Ordinal))
            {
                if (!receipts.TryGetValue(record.CheckpointGeneration, out var receipt)) continue;
                Admit(DevelopmentalProjection(record, receipt, events));
            }
            foreach (var memory in (memories ?? throw new ArgumentNullException(nameof(memories)))
                .OrderBy(value => value.MemoryId, StringComparer.Ordinal))
                Admit(MemoryProjection(memory));
        }

        public string SaveId => _saveId;
        public string WorldId => _worldId;
        public string StoreSetId => _storeSetId;
        public string StoreDigest => _storeDigest;
        public int ProjectionCount => _byId.Count;
        public int FullCanonicalObjectsRetained => 0;
        public int MaximumProjectionsPerId => 1;
        internal long MutationVersion => _mutationVersion;

        public string StateFingerprint
        {
            get
            {
                if (_cachedStateFingerprint is null)
                {
                    _cachedStateFingerprint = RetrievalCanonical.Hash(new[]
                    {
                        RetrievalCanonical.Pair("binding", _saveId + "\n" + _worldId + "\n" + _storeSetId + "\n" + _storeDigest),
                        RetrievalCanonical.Pair("projection_count", _byId.Count.ToString(CultureInfo.InvariantCulture)),
                        RetrievalCanonical.Pair("fingerprint_sum_mod_2_256", Hex(_fingerprintSum)),
                        RetrievalCanonical.Pair("fingerprint_xor", Hex(_fingerprintXor)),
                        RetrievalCanonical.Pair("source_contract", GroundedDevelopmentalContextSource.Contract)
                    });
                }
                return _cachedStateFingerprint;
            }
        }

        internal ContextEvidenceProjection Resolve(DevelopmentalEvidenceKind kind, string evidenceId)
        {
            if (!_byId.TryGetValue(Key(kind, evidenceId), out var projection))
                throw new InvalidOperationException("Selected evidence is absent from the trusted projection registry.");
            return projection;
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

        private static ContextEvidenceProjection DevelopmentalProjection(
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
            var subjects = new HashSet<string>(
                sourceEvent.Subjects.Select(value => value.ToString()), StringComparer.Ordinal);
            if (!subjects.Contains(record.OwnerId) || !subjects.Contains(record.CounterpartId))
                throw new InvalidOperationException("Developmental source event subjects do not match.");
            if (!string.Equals(Payload(sourceEvent, "mosaic_admitted"), "true", StringComparison.Ordinal) ||
                !string.Equals(Payload(sourceEvent, "mosaic_privacy"), "OWNER_PRIVATE", StringComparison.Ordinal) ||
                !string.Equals(Payload(sourceEvent, "mosaic_lineage_id"), record.LineageId, StringComparison.Ordinal))
                throw new InvalidOperationException("Developmental source event trust metadata mismatch.");

            var roots = PayloadList(
                sourceEvent, "mosaic_root_event_ids", new[] { record.SourceEventId }, 8, false);
            var tags = PayloadList(sourceEvent, "mosaic_context_tags", EventTags(sourceEvent), 16, true);
            var dimensions = Dimensions(record).ToList();
            var magnitude = Math.Min(100, dimensions.Sum(value => Math.Min(100, Math.Abs(value.Value) / 10_000)));
            var hasRelationship = dimensions.Any(value => value.Key.StartsWith("relationship.", StringComparison.Ordinal));
            var hasAffect = dimensions.Any(value => value.Key.StartsWith("affect.", StringComparison.Ordinal));
            var category = Payload(sourceEvent, "mosaic_category") ??
                (hasRelationship && hasAffect ? "mixed-social" : hasRelationship ? "relationship" : "affect");
            var durability = Math.Min(100, 25 + magnitude + (hasRelationship ? 25 : 0));
            return new ContextEvidenceProjection(
                DevelopmentalEvidenceKind.Developmental,
                record.RecordId,
                record.OwnerId,
                record.LineageId,
                record.CounterpartId,
                record.SourceEventId,
                roots,
                sourceEvent.GameTick.Value,
                category,
                tags,
                dimensions,
                magnitude,
                durability,
                record.CheckpointGeneration,
                receipt.CheckpointHash,
                storeDigest: null,
                sourceContract: DurableAppraisalAdmissionSource.Contract,
                sourceGateDigest: GroundedDevelopmentalContextSource.V40GateDigest,
                sourceFingerprint: record.Fingerprint());
        }

        private static ContextEvidenceProjection MemoryProjection(ContextualMemoryEvidence memory)
        {
            if (!memory.Admitted || !string.Equals(memory.Privacy, "OWNER_PRIVATE", StringComparison.Ordinal))
                throw new InvalidOperationException("Only admitted owner-private memory projections are accepted.");
            return new ContextEvidenceProjection(
                DevelopmentalEvidenceKind.Memory,
                memory.MemoryId,
                memory.OwnerId,
                memory.LineageId,
                memory.CounterpartId,
                memory.SourceEventId,
                memory.RootEventIds,
                memory.ObservedTick,
                memory.Category,
                memory.Tags,
                Array.Empty<KeyValuePair<string, int>>(),
                memory.Salience,
                Math.Min(100, memory.Salience / 2),
                memory.CheckpointGeneration,
                memory.CheckpointFingerprint,
                null,
                GroundedDevelopmentalContextSource.MemoryEvidenceContract,
                GroundedDevelopmentalContextSource.V41FormalGateDigest,
                memory.Fingerprint);
        }

        private void Admit(ContextEvidenceProjection projection)
        {
            var key = Key(projection.Kind, projection.EvidenceId);
            if (_byId.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Fingerprint, projection.Fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("Conflicting context projection duplicate.");
                return;
            }
            _byId.Add(key, projection);
            var bytes = Bytes(projection.Fingerprint);
            var carry = 0;
            for (var index = bytes.Length - 1; index >= 0; index--)
            {
                var total = _fingerprintSum[index] + bytes[index] + carry;
                _fingerprintSum[index] = (byte)(total & 0xff);
                carry = total >> 8;
                _fingerprintXor[index] ^= bytes[index];
            }
            _mutationVersion++;
            _cachedStateFingerprint = null;
        }

        private static IEnumerable<KeyValuePair<string, int>> Dimensions(DevelopmentalAppraisalRecord record)
        {
            foreach (var value in AffectDimensions(record.ActualAffectDelta))
                if (value.Value != 0) yield return value;
            foreach (var value in RelationshipDimensions(record.ActualRelationshipDelta))
                if (value.Value != 0) yield return value;
        }

        private static IEnumerable<KeyValuePair<string, int>> AffectDimensions(ProvisionalAffectDelta value)
        {
            yield return Dimension("affect.valence", value.Valence);
            yield return Dimension("affect.arousal", value.Arousal);
            yield return Dimension("affect.threat", value.Threat);
            yield return Dimension("affect.agency", value.Agency);
            yield return Dimension("affect.attachment", value.Attachment);
            yield return Dimension("affect.certainty", value.Certainty);
            yield return Dimension("affect.social_standing", value.SocialStanding);
        }

        private static IEnumerable<KeyValuePair<string, int>> RelationshipDimensions(ProvisionalRelationshipDelta value)
        {
            yield return Dimension("relationship.trust", value.Trust);
            yield return Dimension("relationship.affection", value.Affection);
            yield return Dimension("relationship.fear", value.Fear);
            yield return Dimension("relationship.resentment", value.Resentment);
        }

        private static KeyValuePair<string, int> Dimension(string name, decimal value) =>
            new KeyValuePair<string, int>(
                name,
                checked((int)decimal.Truncate(value * GroundedDevelopmentalContextSource.MaximumCanonicalDelta)));

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
            var values = Payload(sourceEvent, key)?.Split(
                new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? fallback.ToArray();
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

        private static string Key(DevelopmentalEvidenceKind kind, string evidenceId) =>
            GroundedTokens.Kind(kind) + "\n" + evidenceId;

        private static byte[] Bytes(string value)
        {
            var result = new byte[32];
            for (var index = 0; index < result.Length; index++)
                result[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return result;
        }

        private static string Hex(IEnumerable<byte> values) =>
            string.Concat(values.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
