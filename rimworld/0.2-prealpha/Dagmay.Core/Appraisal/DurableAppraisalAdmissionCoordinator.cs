using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dagmay.Core.Appraisal
{
    public sealed class DevelopmentalAppraisalRecord
    {
        internal DevelopmentalAppraisalRecord(
            string recordId,
            string packetId,
            string appraisalId,
            string ownerId,
            string lineageId,
            string counterpartId,
            string sourceEventId,
            string sourceEventHash,
            long checkpointGeneration,
            ProvisionalAffectDelta requestedAffectDelta,
            ProvisionalAffectDelta actualAffectDelta,
            ProvisionalRelationshipDelta requestedRelationshipDelta,
            ProvisionalRelationshipDelta actualRelationshipDelta,
            string affectBeforeFingerprint,
            string affectAfterFingerprint,
            string? relationshipBeforeFingerprint,
            string? relationshipAfterFingerprint)
        {
            RecordId = DurableAppraisalCanonical.Hex(recordId, nameof(recordId));
            PacketId = DurableAppraisalCanonical.Hex(packetId, nameof(packetId));
            AppraisalId = DurableAppraisalCanonical.Hex(appraisalId, nameof(appraisalId));
            OwnerId = DurableAppraisalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = DurableAppraisalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = DurableAppraisalCanonical.Text(counterpartId, nameof(counterpartId));
            SourceEventId = DurableAppraisalCanonical.Text(sourceEventId, nameof(sourceEventId));
            SourceEventHash = DurableAppraisalCanonical.Hex(sourceEventHash, nameof(sourceEventHash));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            RequestedAffectDelta = requestedAffectDelta ?? throw new ArgumentNullException(nameof(requestedAffectDelta));
            ActualAffectDelta = actualAffectDelta ?? throw new ArgumentNullException(nameof(actualAffectDelta));
            RequestedRelationshipDelta = requestedRelationshipDelta ?? throw new ArgumentNullException(nameof(requestedRelationshipDelta));
            ActualRelationshipDelta = actualRelationshipDelta ?? throw new ArgumentNullException(nameof(actualRelationshipDelta));
            AffectBeforeFingerprint = DurableAppraisalCanonical.Hex(affectBeforeFingerprint, nameof(affectBeforeFingerprint));
            AffectAfterFingerprint = DurableAppraisalCanonical.Hex(affectAfterFingerprint, nameof(affectAfterFingerprint));
            RelationshipBeforeFingerprint = relationshipBeforeFingerprint is null ? null : DurableAppraisalCanonical.Hex(relationshipBeforeFingerprint, nameof(relationshipBeforeFingerprint));
            RelationshipAfterFingerprint = relationshipAfterFingerprint is null ? null : DurableAppraisalCanonical.Hex(relationshipAfterFingerprint, nameof(relationshipAfterFingerprint));
        }

        public string RecordId { get; }
        public string PacketId { get; }
        public string AppraisalId { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string CounterpartId { get; }
        public string SourceEventId { get; }
        public string SourceEventHash { get; }
        public long CheckpointGeneration { get; }
        public ProvisionalAffectDelta RequestedAffectDelta { get; }
        public ProvisionalAffectDelta ActualAffectDelta { get; }
        public ProvisionalRelationshipDelta RequestedRelationshipDelta { get; }
        public ProvisionalRelationshipDelta ActualRelationshipDelta { get; }
        public string AffectBeforeFingerprint { get; }
        public string AffectAfterFingerprint { get; }
        public string? RelationshipBeforeFingerprint { get; }
        public string? RelationshipAfterFingerprint { get; }
        public DurableAppraisalPrivacy Privacy => DurableAppraisalPrivacy.OwnerPrivate;
        public string Authority => "CANONICAL_RECORD_ONLY_NO_PAWN_AUTHORITY";

        internal bool Same(DevelopmentalAppraisalRecord other) =>
            other is not null && string.Equals(RecordId, other.RecordId, StringComparison.Ordinal) &&
            string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);

        internal string Fingerprint()
        {
            var fields = FieldsWithoutId().ToList();
            fields.Insert(0, DurableAppraisalCanonical.Pair("record_id", RecordId));
            return DurableAppraisalCanonical.Hash(fields);
        }

        internal IEnumerable<KeyValuePair<string, string>> FieldsWithoutId()
        {
            yield return DurableAppraisalCanonical.Pair("schema", DurableAppraisalAdmissionSource.Contract);
            yield return DurableAppraisalCanonical.Pair("packet_id", PacketId);
            yield return DurableAppraisalCanonical.Pair("appraisal_id", AppraisalId);
            yield return DurableAppraisalCanonical.Pair("owner_id", OwnerId);
            yield return DurableAppraisalCanonical.Pair("lineage_id", LineageId);
            yield return DurableAppraisalCanonical.Pair("counterpart_id", CounterpartId);
            yield return DurableAppraisalCanonical.Pair("source_event_id", SourceEventId);
            yield return DurableAppraisalCanonical.Pair("source_event_hash", SourceEventHash);
            yield return DurableAppraisalCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture));
            foreach (var field in RequestedAffectDelta.Fields("requested_affect")) yield return field;
            foreach (var field in ActualAffectDelta.Fields("actual_affect")) yield return field;
            foreach (var field in RequestedRelationshipDelta.Fields("requested_relationship")) yield return field;
            foreach (var field in ActualRelationshipDelta.Fields("actual_relationship")) yield return field;
            yield return DurableAppraisalCanonical.Pair("affect_before", AffectBeforeFingerprint);
            yield return DurableAppraisalCanonical.Pair("affect_after", AffectAfterFingerprint);
            yield return DurableAppraisalCanonical.Pair("relationship_before", RelationshipBeforeFingerprint ?? "null");
            yield return DurableAppraisalCanonical.Pair("relationship_after", RelationshipAfterFingerprint ?? "null");
            yield return DurableAppraisalCanonical.Pair("privacy", Privacy.ToString());
            yield return DurableAppraisalCanonical.Pair("authority", Authority);
        }
    }

    public sealed class DurableAppraisalOutboxEntry
    {
        internal DurableAppraisalOutboxEntry(
            string entryId,
            string requestFingerprint,
            string packetId,
            string ownerId,
            string counterpartId,
            string lineageId,
            string saveId,
            string worldId,
            string storeSetId,
            long checkpointGeneration,
            string checkpointReceiptFingerprint,
            long proposalTick,
            DevelopmentalAppraisalRecord record,
            CanonicalAffectState affectBefore,
            CanonicalAffectState affectAfter,
            CanonicalRelationshipState? relationshipBefore,
            CanonicalRelationshipState? relationshipAfter,
            DurableAppraisalOutboxState state,
            long? completedAtGeneration,
            string? quarantineReason,
            bool failureReceiptIssued)
        {
            EntryId = DurableAppraisalCanonical.Hex(entryId, nameof(entryId));
            RequestFingerprint = DurableAppraisalCanonical.Hex(requestFingerprint, nameof(requestFingerprint));
            PacketId = DurableAppraisalCanonical.Hex(packetId, nameof(packetId));
            OwnerId = DurableAppraisalCanonical.Text(ownerId, nameof(ownerId));
            CounterpartId = DurableAppraisalCanonical.Text(counterpartId, nameof(counterpartId));
            LineageId = DurableAppraisalCanonical.Text(lineageId, nameof(lineageId));
            SaveId = DurableAppraisalCanonical.Text(saveId, nameof(saveId));
            WorldId = DurableAppraisalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = DurableAppraisalCanonical.Text(storeSetId, nameof(storeSetId));
            if (checkpointGeneration < 0 || proposalTick < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            CheckpointReceiptFingerprint = DurableAppraisalCanonical.Hex(checkpointReceiptFingerprint, nameof(checkpointReceiptFingerprint));
            ProposalTick = proposalTick;
            Record = record ?? throw new ArgumentNullException(nameof(record));
            AffectBefore = affectBefore ?? throw new ArgumentNullException(nameof(affectBefore));
            AffectAfter = affectAfter ?? throw new ArgumentNullException(nameof(affectAfter));
            RelationshipBefore = relationshipBefore;
            RelationshipAfter = relationshipAfter;
            State = state;
            CompletedAtGeneration = completedAtGeneration;
            QuarantineReason = quarantineReason;
            FailureReceiptIssued = failureReceiptIssued;
        }

        public string EntryId { get; }
        public string RequestFingerprint { get; }
        public string PacketId { get; }
        public string OwnerId { get; }
        public string CounterpartId { get; }
        public string LineageId { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointReceiptFingerprint { get; }
        public long ProposalTick { get; }
        public DevelopmentalAppraisalRecord Record { get; }
        public CanonicalAffectState AffectBefore { get; }
        public CanonicalAffectState AffectAfter { get; }
        public CanonicalRelationshipState? RelationshipBefore { get; }
        public CanonicalRelationshipState? RelationshipAfter { get; }
        public DurableAppraisalOutboxState State { get; }
        public long? CompletedAtGeneration { get; }
        public string? QuarantineReason { get; }
        public bool FailureReceiptIssued { get; }

        internal DurableAppraisalOutboxEntry Terminal(
            DurableAppraisalOutboxState state, long generation, string? reason = null, bool failureIssued = false) =>
            new DurableAppraisalOutboxEntry(
                EntryId, RequestFingerprint, PacketId, OwnerId, CounterpartId, LineageId,
                SaveId, WorldId, StoreSetId, CheckpointGeneration, CheckpointReceiptFingerprint,
                ProposalTick, Record, AffectBefore, AffectAfter, RelationshipBefore, RelationshipAfter,
                state, generation, reason, failureIssued);

        internal DurableAppraisalOutboxEntry IssueFailureReceipt() =>
            new DurableAppraisalOutboxEntry(
                EntryId, RequestFingerprint, PacketId, OwnerId, CounterpartId, LineageId,
                SaveId, WorldId, StoreSetId, CheckpointGeneration, CheckpointReceiptFingerprint,
                ProposalTick, Record, AffectBefore, AffectAfter, RelationshipBefore, RelationshipAfter,
                State, CompletedAtGeneration, QuarantineReason, true);
    }

    internal sealed class DurableTerminalSeenFilter
    {
        internal const int ByteCount = 1 << 20;
        private const int BitCount = ByteCount * 8;
        private readonly byte[] _bits;

        internal DurableTerminalSeenFilter()
            : this(new byte[ByteCount], 0)
        {
        }

        internal DurableTerminalSeenFilter(byte[] bits, long count)
        {
            if (bits is null || bits.Length != ByteCount) throw new ArgumentException("Terminal filter must contain exactly 1 MiB.", nameof(bits));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            _bits = bits;
            Count = count;
        }

        internal long Count { get; private set; }
        internal byte[] Snapshot() => (byte[])_bits.Clone();

        internal bool Contains(byte[] key)
        {
            foreach (var position in Positions(key))
                if ((_bits[position >> 3] & (1 << (position & 7))) == 0) return false;
            return true;
        }

        internal void Add(byte[] key)
        {
            foreach (var position in Positions(key))
                _bits[position >> 3] |= (byte)(1 << (position & 7));
            Count++;
        }

        internal string Digest
        {
            get
            {
                using (var algorithm = SHA256.Create())
                    return string.Concat(algorithm.ComputeHash(_bits).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static IEnumerable<int> Positions(byte[] key)
        {
            if (key is null || key.Length != 32) throw new ArgumentException("Terminal filter keys require 32 bytes.", nameof(key));
            for (var index = 0; index < 8; index++)
            {
                var offset = index * 4;
                var value = ((uint)key[offset] << 24) | ((uint)key[offset + 1] << 16) |
                            ((uint)key[offset + 2] << 8) | key[offset + 3];
                yield return (int)(value % BitCount);
            }
        }
    }

    public sealed class DurableAppraisalOutbox
    {
        public const int MaximumPending = 64;
        public const int MaximumUnacknowledgedFailures = 64;
        public const int MaximumRecentTerminal = 64;
        public const int TerminalFilterBytes = DurableTerminalSeenFilter.ByteCount;

        internal readonly SortedDictionary<string, DurableAppraisalOutboxEntry> EntriesById =
            new SortedDictionary<string, DurableAppraisalOutboxEntry>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> PacketIndex =
            new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> PendingByOwner =
            new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, Tuple<long, string>> LastOrderByOwner =
            new Dictionary<string, Tuple<long, string>>(StringComparer.Ordinal);
        internal DurableTerminalSeenFilter CompletedFilter = new DurableTerminalSeenFilter();

        public int Count => EntriesById.Count;
        public int PendingCount => EntriesById.Values.Count(value => value.State == DurableAppraisalOutboxState.Pending);
        public int UnacknowledgedFailureCount => EntriesById.Values.Count(value => value.State == DurableAppraisalOutboxState.Quarantined && !value.FailureReceiptIssued);
        public int RecentTerminalCount => EntriesById.Values.Count(value => value.State != DurableAppraisalOutboxState.Pending && (value.State == DurableAppraisalOutboxState.Completed || value.FailureReceiptIssued));
        public long CompletedCompactedCount { get; internal set; }
        public long QuarantinedCount { get; internal set; }
        public string TerminalChain { get; internal set; } = new string('0', 64);
        public long TerminalFilterCount => CompletedFilter.Count;
        public string TerminalFilterDigest => CompletedFilter.Digest;
        public IReadOnlyList<DurableAppraisalOutboxEntry> Entries =>
            new ReadOnlyCollection<DurableAppraisalOutboxEntry>(EntriesById.Values.ToList());

        public string StateDigest()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("schema", DurableAppraisalAdmissionSource.Contract),
                DurableAppraisalCanonical.Pair("completed_compacted", CompletedCompactedCount.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("quarantined_count", QuarantinedCount.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("terminal_chain", TerminalChain),
                DurableAppraisalCanonical.Pair("filter_count", TerminalFilterCount.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("filter_digest", TerminalFilterDigest)
            };
            foreach (var entry in EntriesById.Values)
                fields.Add(DurableAppraisalCanonical.Pair("entry", entry.EntryId + "|" + entry.State + "|" + entry.PacketId));
            foreach (var pair in LastOrderByOwner.OrderBy(value => value.Key, StringComparer.Ordinal))
                fields.Add(DurableAppraisalCanonical.Pair("order", pair.Key + "|" + pair.Value.Item1.ToString(CultureInfo.InvariantCulture) + "|" + pair.Value.Item2));
            return DurableAppraisalCanonical.Hash(fields);
        }

        public int AdvanceCheckpoint(long generation)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            var compacted = 0;
            foreach (var pair in EntriesById.ToArray())
            {
                var entry = pair.Value;
                if (Compactable(entry) && entry.CompletedAtGeneration.HasValue && entry.CompletedAtGeneration.Value < generation)
                {
                    Compact(pair.Key, entry);
                    compacted++;
                }
            }
            while (RecentTerminalCount > MaximumRecentTerminal)
            {
                var pair = EntriesById.First(value => Compactable(value.Value));
                Compact(pair.Key, pair.Value);
                compacted++;
            }
            return compacted;
        }

        internal static string DuplicateKey(string packetId, string ownerId, long generation) =>
            DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("packet", packetId),
                DurableAppraisalCanonical.Pair("owner", ownerId),
                DurableAppraisalCanonical.Pair("checkpoint", generation.ToString(CultureInfo.InvariantCulture))
            });

        private static bool Compactable(DurableAppraisalOutboxEntry entry) =>
            entry.State == DurableAppraisalOutboxState.Completed ||
            (entry.State == DurableAppraisalOutboxState.Quarantined && entry.FailureReceiptIssued);

        private void Compact(string entryId, DurableAppraisalOutboxEntry entry)
        {
            CompletedFilter.Add(DurableAppraisalCanonical.HexBytes(DuplicateKey(entry.PacketId, entry.OwnerId, entry.CheckpointGeneration)));
            if (entry.State == DurableAppraisalOutboxState.Completed) CompletedCompactedCount++;
            TerminalChain = DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("previous", TerminalChain),
                DurableAppraisalCanonical.Pair("entry", entry.EntryId),
                DurableAppraisalCanonical.Pair("packet", entry.PacketId),
                DurableAppraisalCanonical.Pair("record", entry.Record.RecordId),
                DurableAppraisalCanonical.Pair("state", entry.State.ToString()),
                DurableAppraisalCanonical.Pair("generation", entry.CompletedAtGeneration?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                DurableAppraisalCanonical.Pair("failure_receipt_issued", entry.FailureReceiptIssued ? "true" : "false")
            });
            EntriesById.Remove(entryId);
            PacketIndex.Remove(entry.PacketId);
        }
    }

    public sealed class DurableCanonicalStoreSet
    {
        internal readonly Dictionary<string, CanonicalAffectState> AffectByOwner =
            new Dictionary<string, CanonicalAffectState>(StringComparer.Ordinal);
        internal readonly Dictionary<string, CanonicalRelationshipState> RelationshipByPair =
            new Dictionary<string, CanonicalRelationshipState>(StringComparer.Ordinal);
        internal readonly Dictionary<string, DevelopmentalAppraisalRecord> RecordsById =
            new Dictionary<string, DevelopmentalAppraisalRecord>(StringComparer.Ordinal);

        public DurableCanonicalStoreSet(string saveId, string worldId, string storeSetId, bool writable = true, bool healthy = true)
        {
            SaveId = DurableAppraisalCanonical.Text(saveId, nameof(saveId));
            WorldId = DurableAppraisalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = DurableAppraisalCanonical.Text(storeSetId, nameof(storeSetId));
            Writable = writable;
            Healthy = healthy;
        }

        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public bool Writable { get; }
        public bool Healthy { get; }
        public int DevelopmentalRecordCount => RecordsById.Count;
        public IReadOnlyList<DevelopmentalAppraisalRecord> DevelopmentalRecords =>
            new ReadOnlyCollection<DevelopmentalAppraisalRecord>(RecordsById.Values.OrderBy(value => value.RecordId, StringComparer.Ordinal).ToList());

        public void SeedAffect(CanonicalAffectState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (AffectByOwner.ContainsKey(state.OwnerId)) throw new InvalidOperationException("Canonical affect state already exists.");
            AffectByOwner.Add(state.OwnerId, state);
        }

        public void SeedRelationship(CanonicalRelationshipState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            var key = PairKey(state.OwnerId, state.CounterpartId);
            if (RelationshipByPair.ContainsKey(key)) throw new InvalidOperationException("Canonical relationship state already exists.");
            RelationshipByPair.Add(key, state);
        }

        public CanonicalAffectState Affect(string ownerId) => AffectByOwner[ownerId];

        public CanonicalRelationshipState? Relationship(string ownerId, string counterpartId)
        {
            RelationshipByPair.TryGetValue(PairKey(ownerId, counterpartId), out var state);
            return state;
        }

        public string StateDigest()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("save", SaveId),
                DurableAppraisalCanonical.Pair("world", WorldId),
                DurableAppraisalCanonical.Pair("store_set", StoreSetId),
                DurableAppraisalCanonical.Pair("writable", Writable ? "true" : "false"),
                DurableAppraisalCanonical.Pair("healthy", Healthy ? "true" : "false")
            };
            foreach (var item in AffectByOwner.OrderBy(value => value.Key, StringComparer.Ordinal))
                fields.Add(DurableAppraisalCanonical.Pair("affect", item.Key + "|" + item.Value.Fingerprint));
            foreach (var item in RelationshipByPair.OrderBy(value => value.Key, StringComparer.Ordinal))
                fields.Add(DurableAppraisalCanonical.Pair("relationship", item.Key + "|" + item.Value.Fingerprint));
            foreach (var item in RecordsById.OrderBy(value => value.Key, StringComparer.Ordinal))
                fields.Add(DurableAppraisalCanonical.Pair("record", item.Key));
            return DurableAppraisalCanonical.Hash(fields);
        }

        internal static string PairKey(string ownerId, string counterpartId) => ownerId + "\n" + counterpartId;
    }

    public sealed class CanonicalDurableAdmissionEvidence
    {
        internal CanonicalDurableAdmissionEvidence(
            string entryId,
            string developmentalRecordId,
            ProvisionalAffectDelta actualAffectDelta,
            ProvisionalRelationshipDelta actualRelationshipDelta,
            CanonicalApplicationReceipt applicationReceipt,
            string completionFingerprint)
        {
            EntryId = DurableAppraisalCanonical.Hex(entryId, nameof(entryId));
            DevelopmentalRecordId = DurableAppraisalCanonical.Hex(developmentalRecordId, nameof(developmentalRecordId));
            ActualAffectDelta = actualAffectDelta ?? throw new ArgumentNullException(nameof(actualAffectDelta));
            ActualRelationshipDelta = actualRelationshipDelta ?? throw new ArgumentNullException(nameof(actualRelationshipDelta));
            ApplicationReceipt = applicationReceipt ?? throw new ArgumentNullException(nameof(applicationReceipt));
            CompletionFingerprint = DurableAppraisalCanonical.Hex(completionFingerprint, nameof(completionFingerprint));
        }

        public string EntryId { get; }
        public string DevelopmentalRecordId { get; }
        public ProvisionalAffectDelta ActualAffectDelta { get; }
        public ProvisionalRelationshipDelta ActualRelationshipDelta { get; }
        public CanonicalApplicationReceipt ApplicationReceipt { get; }
        public string SourceContract => DurableAppraisalAdmissionSource.Contract;
        public string CompletionFingerprint { get; }
        public bool IsCanonicalSuccess => true;
    }

    public sealed class CheckpointAlignedDurableAppraisalCoordinator
    {
        private readonly object _gate = new object();
        private readonly DurableCanonicalStoreSet _stores;
        private readonly DurableAppraisalOutbox _outbox;
        private readonly DurableProposalRegistry _registry;

        public CheckpointAlignedDurableAppraisalCoordinator(
            DurableCanonicalStoreSet stores,
            DurableAppraisalOutbox outbox,
            DurableProposalRegistry registry)
        {
            _stores = stores ?? throw new ArgumentNullException(nameof(stores));
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public DurableAdmissionAttemptReceipt Prepare(
            DurableAppraisalAdmissionRequest request,
            DurableAdmissionFaultStage? faultAfter = null)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            lock (_gate)
            {
                var packet = request.Proposal.Packet;
                var owner = packet.PerspectiveOwnerId;
                var requestFingerprint = RequestFingerprint(request);
                if (_outbox.PacketIndex.TryGetValue(packet.PacketId, out var existingId))
                {
                    if (!_outbox.EntriesById.TryGetValue(existingId, out var existing))
                        throw new ArgumentException("Terminal duplicate proposal already compacted.", nameof(request));
                    if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                        throw new ArgumentException("Conflicting duplicate durable proposal.", nameof(request));
                    return DurableAdmissionAttemptReceipt.Create(
                        existing.EntryId, existing.PacketId,
                        existing.State == DurableAppraisalOutboxState.Quarantined
                            ? DurableAdmissionAttemptOutcome.PermanentFailure
                            : DurableAdmissionAttemptOutcome.Prepared,
                        existing.CheckpointGeneration);
                }
                var duplicateKey = DurableAppraisalOutbox.DuplicateKey(
                    packet.PacketId, owner, packet.ExpectedCheckpointGeneration);
                if (_outbox.CompletedFilter.Contains(DurableAppraisalCanonical.HexBytes(duplicateKey)))
                    throw new ArgumentException("Proposal may already be terminal; duplicate filter fails closed.", nameof(request));
                Validate(request);
                if (_outbox.UnacknowledgedFailureCount >= DurableAppraisalOutbox.MaximumUnacknowledgedFailures)
                    throw new InvalidOperationException("Unacknowledged permanent-failure backlog is full.");
                if (_outbox.PendingCount >= DurableAppraisalOutbox.MaximumPending)
                    throw new InvalidOperationException("Global pending durable-admission queue is full.");
                if (_outbox.PendingByOwner.ContainsKey(owner))
                    throw new InvalidOperationException("Owner already has an in-flight durable admission.");
                var order = Tuple.Create(request.Proposal.CreatedTick, packet.PacketId);
                if (_outbox.LastOrderByOwner.TryGetValue(owner, out var previous) && Compare(order, previous) <= 0)
                    throw new ArgumentException("Out-of-order or stale durable proposal.", nameof(request));

                if (!_stores.AffectByOwner.TryGetValue(owner, out var beforeAffect))
                    throw new InvalidOperationException("Missing canonical affect state.");
                if (beforeAffect.Version != packet.ExpectedAffectVersion ||
                    !string.Equals(beforeAffect.Fingerprint, packet.ExpectedAffectFingerprint, StringComparison.Ordinal))
                    throw new ArgumentException("Stale canonical affect state.", nameof(request));

                var pairKey = DurableCanonicalStoreSet.PairKey(owner, request.Proposal.SpeakerId);
                _stores.RelationshipByPair.TryGetValue(pairKey, out var beforeRelationship);
                if (!packet.ExpectedRelationshipVersion.HasValue)
                {
                    if (beforeRelationship is not null || !packet.ProposedRelationshipDelta.IsZero)
                        throw new ArgumentException("Relationship proposal/state presence mismatch.", nameof(request));
                }
                else if (beforeRelationship is null ||
                         beforeRelationship.Version != packet.ExpectedRelationshipVersion.Value ||
                         !string.Equals(beforeRelationship.Fingerprint, packet.ExpectedRelationshipFingerprint, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Stale canonical relationship state.", nameof(request));
                }

                var afterAffect = beforeAffect.Apply(packet.ProposedAffectDelta, out var actualAffect);
                CanonicalRelationshipState? afterRelationship = null;
                var actualRelationship = ProvisionalRelationshipDelta.Zero;
                if (beforeRelationship is not null)
                    afterRelationship = beforeRelationship.Apply(packet.ProposedRelationshipDelta, out actualRelationship);

                var recordFields = RecordFields(
                    packet, request, beforeAffect, afterAffect, beforeRelationship, afterRelationship,
                    actualAffect, actualRelationship);
                var recordId = DurableAppraisalCanonical.Hash(recordFields);
                var record = new DevelopmentalAppraisalRecord(
                    recordId, packet.PacketId, packet.AppraisalId, owner, request.Identity.LineageId,
                    request.Proposal.SpeakerId, request.EventReceipt.EventId, request.EventReceipt.EventHash,
                    packet.ExpectedCheckpointGeneration, packet.ProposedAffectDelta, actualAffect,
                    packet.ProposedRelationshipDelta, actualRelationship, beforeAffect.Fingerprint,
                    afterAffect.Fingerprint, beforeRelationship?.Fingerprint, afterRelationship?.Fingerprint);
                var entryId = DurableAppraisalCanonical.Hash(new[]
                {
                    DurableAppraisalCanonical.Pair("schema", DurableAppraisalAdmissionSource.Contract),
                    DurableAppraisalCanonical.Pair("request", requestFingerprint),
                    DurableAppraisalCanonical.Pair("checkpoint_receipt_fingerprint", request.CheckpointReceipt.Fingerprint),
                    DurableAppraisalCanonical.Pair("record", record.RecordId),
                    DurableAppraisalCanonical.Pair("affect_after", afterAffect.Fingerprint),
                    DurableAppraisalCanonical.Pair("relationship_after", afterRelationship?.Fingerprint ?? "null"),
                    DurableAppraisalCanonical.Pair("save", _stores.SaveId),
                    DurableAppraisalCanonical.Pair("world", _stores.WorldId),
                    DurableAppraisalCanonical.Pair("store_set", _stores.StoreSetId)
                });
                var entry = new DurableAppraisalOutboxEntry(
                    entryId, requestFingerprint, packet.PacketId, owner, request.Proposal.SpeakerId,
                    request.Identity.LineageId, _stores.SaveId, _stores.WorldId, _stores.StoreSetId,
                    packet.ExpectedCheckpointGeneration, request.CheckpointReceipt.Fingerprint,
                    request.Proposal.CreatedTick, record, beforeAffect, afterAffect,
                    beforeRelationship, afterRelationship, DurableAppraisalOutboxState.Pending,
                    null, null, false);
                _outbox.EntriesById.Add(entry.EntryId, entry);
                _outbox.PacketIndex.Add(entry.PacketId, entry.EntryId);
                _outbox.PendingByOwner.Add(entry.OwnerId, entry.EntryId);
                _outbox.LastOrderByOwner[entry.OwnerId] = order;
                _registry.Consume(request.Proposal);
                if (faultAfter == DurableAdmissionFaultStage.AfterOutbox)
                    throw new DurableAdmissionSimulatedCrash(DurableAdmissionFaultStage.AfterOutbox);
                return DurableAdmissionAttemptReceipt.Create(
                    entry.EntryId, entry.PacketId, DurableAdmissionAttemptOutcome.Prepared,
                    entry.CheckpointGeneration);
            }
        }

        public CanonicalDurableAdmissionEvidence Apply(
            string entryId,
            CheckpointCommitReceipt checkpoint,
            DurableAdmissionFaultStage? faultAfter = null)
        {
            DurableAppraisalCanonical.Hex(entryId, nameof(entryId));
            if (checkpoint is null) throw new ArgumentNullException(nameof(checkpoint));
            lock (_gate)
            {
                if (!_outbox.EntriesById.TryGetValue(entryId, out var entry))
                    throw new ArgumentException("Unknown or compacted outbox entry.", nameof(entryId));
                if (entry.State == DurableAppraisalOutboxState.Quarantined)
                    throw new InvalidOperationException("Outbox entry is quarantined.");
                if (entry.State == DurableAppraisalOutboxState.Completed)
                    return Evidence(entry);
                ValidateRecoveryCheckpoint(entry, checkpoint);
                try
                {
                    _stores.RecordsById.TryGetValue(entry.Record.RecordId, out var existingRecord);
                    _stores.AffectByOwner.TryGetValue(entry.OwnerId, out var currentAffect);
                    CanonicalRelationshipState? currentRelationship = null;
                    if (entry.RelationshipBefore is not null)
                        _stores.RelationshipByPair.TryGetValue(
                            DurableCanonicalStoreSet.PairKey(entry.OwnerId, entry.CounterpartId),
                            out currentRelationship);

                    // Complete conflict preflight under this same coordinator lock.
                    if (existingRecord is not null && !existingRecord.Same(entry.Record))
                        throw new InvalidOperationException("Conflicting developmental-record destination.");
                    if (currentAffect is null ||
                        (!currentAffect.Same(entry.AffectBefore) && !currentAffect.Same(entry.AffectAfter)))
                        throw new InvalidOperationException("Conflicting affect destination.");
                    if (entry.RelationshipBefore is not null &&
                        (currentRelationship is null ||
                         (!currentRelationship.Same(entry.RelationshipBefore) &&
                          !currentRelationship.Same(entry.RelationshipAfter!))))
                        throw new InvalidOperationException("Conflicting relationship destination.");

                    if (existingRecord is null) _stores.RecordsById.Add(entry.Record.RecordId, entry.Record);
                    Crash(faultAfter, DurableAdmissionFaultStage.AfterDevelopmentalRecord);
                    if (currentAffect.Same(entry.AffectBefore))
                        _stores.AffectByOwner[entry.OwnerId] = entry.AffectAfter;
                    Crash(faultAfter, DurableAdmissionFaultStage.AfterAffect);
                    if (entry.RelationshipBefore is not null && entry.RelationshipAfter is not null &&
                        currentRelationship!.Same(entry.RelationshipBefore))
                        _stores.RelationshipByPair[DurableCanonicalStoreSet.PairKey(entry.OwnerId, entry.CounterpartId)] = entry.RelationshipAfter;
                    Crash(faultAfter, DurableAdmissionFaultStage.AfterRelationship);

                    if (!_stores.RecordsById.TryGetValue(entry.Record.RecordId, out var rereadRecord) ||
                        !rereadRecord.Same(entry.Record) ||
                        !_stores.AffectByOwner.TryGetValue(entry.OwnerId, out var rereadAffect) ||
                        !rereadAffect.Same(entry.AffectAfter) ||
                        (entry.RelationshipAfter is not null &&
                         (!_stores.RelationshipByPair.TryGetValue(
                             DurableCanonicalStoreSet.PairKey(entry.OwnerId, entry.CounterpartId), out var rereadRelationship) ||
                          !rereadRelationship.Same(entry.RelationshipAfter))))
                        throw new InvalidOperationException("Post-write reread verification failed.");
                    Crash(faultAfter, DurableAdmissionFaultStage.BeforeCompletion);
                    entry = entry.Terminal(DurableAppraisalOutboxState.Completed, checkpoint.CheckpointGeneration);
                    _outbox.EntriesById[entry.EntryId] = entry;
                    _outbox.PendingByOwner.Remove(entry.OwnerId);
                    _outbox.AdvanceCheckpoint(checkpoint.CheckpointGeneration);
                    Crash(faultAfter, DurableAdmissionFaultStage.AfterCompletion);
                    return Evidence(entry);
                }
                catch (DurableAdmissionSimulatedCrash)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var quarantined = entry.Terminal(
                        DurableAppraisalOutboxState.Quarantined, entry.CheckpointGeneration,
                        exception.GetType().Name + ":" + exception.Message);
                    _outbox.EntriesById[entry.EntryId] = quarantined;
                    _outbox.PendingByOwner.Remove(entry.OwnerId);
                    _outbox.QuarantinedCount++;
                    throw;
                }
            }
        }

        public CanonicalApplicationReceipt PermanentFailureReceipt(string entryId)
        {
            lock (_gate)
            {
                if (!_outbox.EntriesById.TryGetValue(entryId, out var entry) ||
                    entry.State != DurableAppraisalOutboxState.Quarantined)
                    throw new ArgumentException("Only quarantined entries produce permanent-failure receipts.", nameof(entryId));
                if (!entry.FailureReceiptIssued)
                {
                    entry = entry.IssueFailureReceipt();
                    _outbox.EntriesById[entry.EntryId] = entry;
                    _outbox.AdvanceCheckpoint(entry.CheckpointGeneration);
                }
                return CanonicalApplicationReceipt.CreateTrusted(
                    entry.PacketId, entry.OwnerId, entry.Record.SourceEventId, entry.CheckpointGeneration,
                    false, entry.AffectBefore.Fingerprint, entry.AffectBefore.Version,
                    entry.RelationshipBefore?.Fingerprint, entry.RelationshipBefore?.Version,
                    CanonicalApplicationReceipt.SourceContractValue);
            }
        }

        public IReadOnlyDictionary<string, string> DiagnosticProjection()
        {
            lock (_gate)
            {
                var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["schema"] = DurableAppraisalAdmissionSource.Contract,
                    ["binding_hash"] = DurableAppraisalCanonical.Hash(new[]
                    {
                        DurableAppraisalCanonical.Pair("save", _stores.SaveId),
                        DurableAppraisalCanonical.Pair("world", _stores.WorldId),
                        DurableAppraisalCanonical.Pair("store", _stores.StoreSetId)
                    }),
                    ["pending"] = _outbox.PendingCount.ToString(CultureInfo.InvariantCulture),
                    ["quarantined"] = _outbox.QuarantinedCount.ToString(CultureInfo.InvariantCulture),
                    ["completed_compacted"] = _outbox.CompletedCompactedCount.ToString(CultureInfo.InvariantCulture),
                    ["outbox_digest"] = _outbox.StateDigest(),
                    ["canonical_digest"] = _stores.StateDigest(),
                    ["authority"] = DurableAppraisalAdmissionSource.NoPawnAuthority
                };
                return new ReadOnlyDictionary<string, string>(fields);
            }
        }

        private void Validate(DurableAppraisalAdmissionRequest request)
        {
            _registry.Require(request.Proposal);
            var packet = request.Proposal.Packet;
            if (!string.Equals(request.EventReceipt.Fingerprint, request.EventReceipt.ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Forged dialogue-event receipt.", nameof(request));
            if (!string.Equals(request.CheckpointReceipt.Fingerprint, request.CheckpointReceipt.ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Forged checkpoint receipt.", nameof(request));
            if (!request.CheckpointReceipt.Completed || !request.CheckpointReceipt.Writable || !request.CheckpointReceipt.StoresHealthy ||
                !_stores.Writable || !_stores.Healthy)
                throw new InvalidOperationException("Checkpoint and canonical stores must be complete, writable, and healthy.");
            if (!SameBinding(request.Identity.SaveId, request.Identity.WorldId, request.Identity.StoreSetId) ||
                !SameBinding(request.EventReceipt.SaveId, request.EventReceipt.WorldId, request.EventReceipt.StoreSetId) ||
                !SameBinding(request.CheckpointReceipt.SaveId, request.CheckpointReceipt.WorldId, request.CheckpointReceipt.StoreSetId))
                throw new ArgumentException("Foreign save, world, or store-set binding.", nameof(request));
            if (!string.Equals(packet.PerspectiveOwnerId, request.Identity.IndividualId, StringComparison.Ordinal) ||
                !string.Equals(request.EventReceipt.OwnerId, request.Identity.IndividualId, StringComparison.Ordinal))
                throw new ArgumentException("Owner identity mismatch.", nameof(request));
            if (!string.Equals(packet.AdmittedDialogueEventId, request.EventReceipt.EventId, StringComparison.Ordinal) ||
                !string.Equals(packet.SourceReceiptId, request.EventReceipt.ReceiptId, StringComparison.Ordinal))
                throw new ArgumentException("Proposal does not match the admitted dialogue-event receipt.", nameof(request));
            if (!string.Equals(request.Proposal.SpeakerId, request.EventReceipt.SpeakerId, StringComparison.Ordinal))
                throw new ArgumentException("Speaker identity mismatch.", nameof(request));
            if (packet.ExpectedCheckpointGeneration != request.CheckpointReceipt.CheckpointGeneration ||
                request.EventReceipt.CheckpointGeneration != request.CheckpointReceipt.CheckpointGeneration)
                throw new ArgumentException("Stale or mismatched checkpoint generation.", nameof(request));
            if (request.EventReceipt.EventTick > request.Proposal.CreatedTick)
                throw new ArgumentException("A future dialogue event cannot ground an earlier appraisal.", nameof(request));
            if (packet.ProposedAffectDelta.MaximumAbsolute > ProvisionalDialogueAppraisalStore.PerUtteranceAffectBound ||
                packet.ProposedRelationshipDelta.MaximumAbsolute > ProvisionalDialogueAppraisalStore.PerUtteranceRelationshipBound)
                throw new ArgumentException("Proposal exceeds corrected-v39 durable bounds.", nameof(request));
        }

        private void ValidateRecoveryCheckpoint(DurableAppraisalOutboxEntry entry, CheckpointCommitReceipt checkpoint)
        {
            if (!string.Equals(checkpoint.Fingerprint, checkpoint.ComputeFingerprint(), StringComparison.Ordinal) ||
                !checkpoint.Completed || !checkpoint.Writable || !checkpoint.StoresHealthy ||
                checkpoint.CheckpointGeneration != entry.CheckpointGeneration ||
                !SameBinding(checkpoint.SaveId, checkpoint.WorldId, checkpoint.StoreSetId) ||
                !string.Equals(checkpoint.Fingerprint, entry.CheckpointReceiptFingerprint, StringComparison.Ordinal) ||
                !_stores.Writable || !_stores.Healthy)
                throw new ArgumentException("Checkpoint cannot authorize this exact durable application.", nameof(checkpoint));
        }

        private bool SameBinding(string saveId, string worldId, string storeSetId) =>
            string.Equals(saveId, _stores.SaveId, StringComparison.Ordinal) &&
            string.Equals(worldId, _stores.WorldId, StringComparison.Ordinal) &&
            string.Equals(storeSetId, _stores.StoreSetId, StringComparison.Ordinal);

        private CanonicalDurableAdmissionEvidence Evidence(DurableAppraisalOutboxEntry entry)
        {
            if (entry.State != DurableAppraisalOutboxState.Completed)
                throw new InvalidOperationException("An attempt is not canonical success.");
            var receipt = CanonicalApplicationReceipt.CreateTrusted(
                entry.PacketId, entry.OwnerId, entry.Record.SourceEventId, entry.CheckpointGeneration,
                true, entry.AffectAfter.Fingerprint, entry.AffectAfter.Version,
                entry.RelationshipAfter?.Fingerprint, entry.RelationshipAfter?.Version,
                CanonicalApplicationReceipt.SourceContractValue);
            var completion = DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("entry_id", entry.EntryId),
                DurableAppraisalCanonical.Pair("record_id", entry.Record.RecordId),
                DurableAppraisalCanonical.Pair("receipt", receipt.ReceiptFingerprint),
                DurableAppraisalCanonical.Pair("source_contract", DurableAppraisalAdmissionSource.Contract)
            });
            return new CanonicalDurableAdmissionEvidence(
                entry.EntryId, entry.Record.RecordId, entry.Record.ActualAffectDelta,
                entry.Record.ActualRelationshipDelta, receipt, completion);
        }

        private static string RequestFingerprint(DurableAppraisalAdmissionRequest request) =>
            DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("proposal", request.Proposal.Fingerprint),
                DurableAppraisalCanonical.Pair("identity", request.Identity.Fingerprint),
                DurableAppraisalCanonical.Pair("event", request.EventReceipt.Fingerprint),
                DurableAppraisalCanonical.Pair("checkpoint", request.CheckpointReceipt.Fingerprint)
            });

        private static IEnumerable<KeyValuePair<string, string>> RecordFields(
            DurableApplicationPacket packet,
            DurableAppraisalAdmissionRequest request,
            CanonicalAffectState beforeAffect,
            CanonicalAffectState afterAffect,
            CanonicalRelationshipState? beforeRelationship,
            CanonicalRelationshipState? afterRelationship,
            ProvisionalAffectDelta actualAffect,
            ProvisionalRelationshipDelta actualRelationship)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("schema", DurableAppraisalAdmissionSource.Contract),
                DurableAppraisalCanonical.Pair("packet_id", packet.PacketId),
                DurableAppraisalCanonical.Pair("appraisal_id", packet.AppraisalId),
                DurableAppraisalCanonical.Pair("owner_id", packet.PerspectiveOwnerId),
                DurableAppraisalCanonical.Pair("lineage_id", request.Identity.LineageId),
                DurableAppraisalCanonical.Pair("counterpart_id", request.Proposal.SpeakerId),
                DurableAppraisalCanonical.Pair("source_event_id", request.EventReceipt.EventId),
                DurableAppraisalCanonical.Pair("source_event_hash", request.EventReceipt.EventHash),
                DurableAppraisalCanonical.Pair("checkpoint_generation", packet.ExpectedCheckpointGeneration.ToString(CultureInfo.InvariantCulture))
            };
            fields.AddRange(packet.ProposedAffectDelta.Fields("requested_affect"));
            fields.AddRange(actualAffect.Fields("actual_affect"));
            fields.AddRange(packet.ProposedRelationshipDelta.Fields("requested_relationship"));
            fields.AddRange(actualRelationship.Fields("actual_relationship"));
            fields.Add(DurableAppraisalCanonical.Pair("affect_before", beforeAffect.Fingerprint));
            fields.Add(DurableAppraisalCanonical.Pair("affect_after", afterAffect.Fingerprint));
            fields.Add(DurableAppraisalCanonical.Pair("relationship_before", beforeRelationship?.Fingerprint ?? "null"));
            fields.Add(DurableAppraisalCanonical.Pair("relationship_after", afterRelationship?.Fingerprint ?? "null"));
            fields.Add(DurableAppraisalCanonical.Pair("privacy", DurableAppraisalPrivacy.OwnerPrivate.ToString()));
            fields.Add(DurableAppraisalCanonical.Pair("authority", "CANONICAL_RECORD_ONLY_NO_PAWN_AUTHORITY"));
            return fields;
        }

        private static int Compare(Tuple<long, string> left, Tuple<long, string> right)
        {
            var tick = left.Item1.CompareTo(right.Item1);
            return tick != 0 ? tick : string.Compare(left.Item2, right.Item2, StringComparison.Ordinal);
        }

        private static void Crash(DurableAdmissionFaultStage? requested, DurableAdmissionFaultStage stage)
        {
            if (requested == stage) throw new DurableAdmissionSimulatedCrash(stage);
        }
    }
}
