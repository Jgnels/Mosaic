using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dagmay.Core.Appraisal
{
    public sealed class DurableAppraisalOutboxCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MOSAIC-03E-OUTBOX-SNAPSHOT-V3\n");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public byte[] Encode(DurableCanonicalStoreSet stores, DurableAppraisalOutbox outbox)
        {
            if (stores is null) throw new ArgumentNullException(nameof(stores));
            if (outbox is null) throw new ArgumentNullException(nameof(outbox));
            byte[] payload;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, StrictUtf8, true))
                {
                    writer.Write(DurableAppraisalAdmissionSource.Contract);
                    writer.Write(stores.SaveId);
                    writer.Write(stores.WorldId);
                    writer.Write(stores.StoreSetId);
                    writer.Write(outbox.EntriesById.Count);
                    foreach (var entry in outbox.EntriesById.Values) WriteEntry(writer, entry);
                    writer.Write(outbox.PacketIndex.Count);
                    foreach (var pair in outbox.PacketIndex.OrderBy(value => value.Key, StringComparer.Ordinal))
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }
                    writer.Write(outbox.PendingByOwner.Count);
                    foreach (var pair in outbox.PendingByOwner.OrderBy(value => value.Key, StringComparer.Ordinal))
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }
                    writer.Write(outbox.LastOrderByOwner.Count);
                    foreach (var pair in outbox.LastOrderByOwner.OrderBy(value => value.Key, StringComparer.Ordinal))
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value.Item1);
                        writer.Write(pair.Value.Item2);
                    }
                    var bits = outbox.CompletedFilter.Snapshot();
                    writer.Write(bits.Length);
                    writer.Write(bits);
                    writer.Write(outbox.CompletedFilter.Count);
                    writer.Write(outbox.CompletedCompactedCount);
                    writer.Write(outbox.TerminalChain);
                    writer.Write(outbox.QuarantinedCount);
                }
                payload = stream.ToArray();
            }
            string hash;
            using (var algorithm = SHA256.Create())
                hash = string.Concat(algorithm.ComputeHash(payload).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            using (var result = new MemoryStream())
            {
                result.Write(Magic, 0, Magic.Length);
                var header = Encoding.ASCII.GetBytes(hash + "\n");
                result.Write(header, 0, header.Length);
                result.Write(payload, 0, payload.Length);
                return result.ToArray();
            }
        }

        public DurableAppraisalOutbox Decode(byte[] bytes, DurableCanonicalStoreSet stores)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            if (stores is null) throw new ArgumentNullException(nameof(stores));
            if (bytes.Length < Magic.Length + 65 || !bytes.Take(Magic.Length).SequenceEqual(Magic))
                throw new InvalidDataException("Durable appraisal outbox magic mismatch.");
            var expected = StrictUtf8.GetString(bytes, Magic.Length, 64);
            if (bytes[Magic.Length + 64] != (byte)'\n')
                throw new InvalidDataException("Durable appraisal outbox header is malformed.");
            var payload = bytes.Skip(Magic.Length + 65).ToArray();
            string actual;
            using (var algorithm = SHA256.Create())
                actual = string.Concat(algorithm.ComputeHash(payload).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            if (!FixedTimeEquals(expected, actual))
                throw new InvalidDataException("Durable appraisal outbox payload hash mismatch.");

            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, StrictUtf8, true))
            {
                if (!string.Equals(reader.ReadString(), DurableAppraisalAdmissionSource.Contract, StringComparison.Ordinal))
                    throw new InvalidDataException("Durable appraisal outbox schema mismatch.");
                var save = reader.ReadString();
                var world = reader.ReadString();
                var storeSet = reader.ReadString();
                if (!string.Equals(save, stores.SaveId, StringComparison.Ordinal) ||
                    !string.Equals(world, stores.WorldId, StringComparison.Ordinal) ||
                    !string.Equals(storeSet, stores.StoreSetId, StringComparison.Ordinal))
                    throw new InvalidDataException("Durable appraisal outbox belongs to foreign canonical stores.");
                var outbox = new DurableAppraisalOutbox();
                var entryCount = BoundedCount(reader, "entry", 1024);
                for (var index = 0; index < entryCount; index++)
                {
                    var entry = ReadEntry(reader);
                    if (outbox.EntriesById.ContainsKey(entry.EntryId))
                        throw new InvalidDataException("Duplicate durable appraisal outbox entry.");
                    outbox.EntriesById.Add(entry.EntryId, entry);
                }
                var packetCount = BoundedCount(reader, "packet index", 1024);
                for (var index = 0; index < packetCount; index++)
                    outbox.PacketIndex.Add(reader.ReadString(), reader.ReadString());
                var pendingCount = BoundedCount(reader, "pending index", DurableAppraisalOutbox.MaximumPending);
                for (var index = 0; index < pendingCount; index++)
                    outbox.PendingByOwner.Add(reader.ReadString(), reader.ReadString());
                var orderCount = BoundedCount(reader, "owner order", 1_000_000);
                for (var index = 0; index < orderCount; index++)
                    outbox.LastOrderByOwner.Add(reader.ReadString(), Tuple.Create(reader.ReadInt64(), reader.ReadString()));
                var bitCount = reader.ReadInt32();
                if (bitCount != DurableAppraisalOutbox.TerminalFilterBytes)
                    throw new InvalidDataException("Terminal duplicate filter byte count mismatch.");
                var bits = reader.ReadBytes(bitCount);
                if (bits.Length != bitCount) throw new EndOfStreamException();
                var seenCount = reader.ReadInt64();
                outbox.CompletedFilter = new DurableTerminalSeenFilter(bits, seenCount);
                outbox.CompletedCompactedCount = reader.ReadInt64();
                outbox.TerminalChain = DurableAppraisalCanonical.Hex(reader.ReadString(), "terminal chain");
                outbox.QuarantinedCount = reader.ReadInt64();
                if (outbox.CompletedCompactedCount < 0 || outbox.QuarantinedCount < 0)
                    throw new InvalidDataException("Negative durable appraisal terminal count.");
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Trailing durable appraisal outbox bytes.");
                ValidateIndexes(outbox);
                return outbox;
            }
        }

        private static void ValidateIndexes(DurableAppraisalOutbox outbox)
        {
            foreach (var entry in outbox.EntriesById.Values)
            {
                if (!outbox.PacketIndex.TryGetValue(entry.PacketId, out var entryId) ||
                    !string.Equals(entryId, entry.EntryId, StringComparison.Ordinal))
                    throw new InvalidDataException("Durable appraisal packet index mismatch.");
                if (entry.State == DurableAppraisalOutboxState.Pending)
                {
                    if (!outbox.PendingByOwner.TryGetValue(entry.OwnerId, out var pendingId) ||
                        !string.Equals(pendingId, entry.EntryId, StringComparison.Ordinal))
                        throw new InvalidDataException("Durable appraisal pending index mismatch.");
                }
                else if (outbox.PendingByOwner.TryGetValue(entry.OwnerId, out var pendingId) &&
                         string.Equals(pendingId, entry.EntryId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Terminal entry remains in the pending index.");
                }
                if (outbox.LastOrderByOwner.TryGetValue(entry.OwnerId, out var order) &&
                    (order.Item1 < entry.ProposalTick ||
                     (order.Item1 == entry.ProposalTick && string.Compare(order.Item2, entry.PacketId, StringComparison.Ordinal) < 0)))
                    throw new InvalidDataException("Durable appraisal owner order predates an entry.");
            }
            foreach (var pair in outbox.PacketIndex)
                if (!outbox.EntriesById.TryGetValue(pair.Value, out var entry) ||
                    !string.Equals(entry.PacketId, pair.Key, StringComparison.Ordinal))
                    throw new InvalidDataException("Durable appraisal packet index mismatch.");
            if (outbox.PendingCount > DurableAppraisalOutbox.MaximumPending ||
                outbox.UnacknowledgedFailureCount > DurableAppraisalOutbox.MaximumUnacknowledgedFailures ||
                outbox.RecentTerminalCount > DurableAppraisalOutbox.MaximumRecentTerminal)
                throw new InvalidDataException("Durable appraisal outbox exceeds fixed lifecycle bounds.");
        }

        private static void WriteEntry(BinaryWriter writer, DurableAppraisalOutboxEntry entry)
        {
            writer.Write(entry.EntryId);
            writer.Write(entry.RequestFingerprint);
            writer.Write(entry.PacketId);
            writer.Write(entry.OwnerId);
            writer.Write(entry.CounterpartId);
            writer.Write(entry.LineageId);
            writer.Write(entry.SaveId);
            writer.Write(entry.WorldId);
            writer.Write(entry.StoreSetId);
            writer.Write(entry.CheckpointGeneration);
            writer.Write(entry.CheckpointReceiptFingerprint);
            writer.Write(entry.ProposalTick);
            WriteRecord(writer, entry.Record);
            WriteAffect(writer, entry.AffectBefore);
            WriteAffect(writer, entry.AffectAfter);
            WriteRelationship(writer, entry.RelationshipBefore);
            WriteRelationship(writer, entry.RelationshipAfter);
            writer.Write((int)entry.State);
            WriteNullableInt64(writer, entry.CompletedAtGeneration);
            WriteNullableString(writer, entry.QuarantineReason);
            writer.Write(entry.FailureReceiptIssued);
        }

        private static DurableAppraisalOutboxEntry ReadEntry(BinaryReader reader)
        {
            var entryId = reader.ReadString();
            var requestFingerprint = reader.ReadString();
            var packetId = reader.ReadString();
            var ownerId = reader.ReadString();
            var counterpartId = reader.ReadString();
            var lineageId = reader.ReadString();
            var saveId = reader.ReadString();
            var worldId = reader.ReadString();
            var storeSetId = reader.ReadString();
            var generation = reader.ReadInt64();
            var checkpointFingerprint = reader.ReadString();
            var proposalTick = reader.ReadInt64();
            var record = ReadRecord(reader);
            var affectBefore = ReadAffect(reader);
            var affectAfter = ReadAffect(reader);
            var relationshipBefore = ReadRelationship(reader);
            var relationshipAfter = ReadRelationship(reader);
            var state = (DurableAppraisalOutboxState)reader.ReadInt32();
            if (!Enum.IsDefined(typeof(DurableAppraisalOutboxState), state))
                throw new InvalidDataException("Undefined durable appraisal outbox state.");
            var completed = ReadNullableInt64(reader);
            var reason = ReadNullableString(reader);
            var issued = reader.ReadBoolean();
            var expectedRecord = DurableAppraisalCanonical.Hash(record.FieldsWithoutId());
            if (!string.Equals(expectedRecord, record.RecordId, StringComparison.Ordinal))
                throw new InvalidDataException("Developmental appraisal record ID mismatch.");
            var expectedEntry = ComputeEntryId(
                requestFingerprint, checkpointFingerprint, record.RecordId, affectAfter.Fingerprint,
                relationshipAfter?.Fingerprint, saveId, worldId, storeSetId);
            if (!string.Equals(expectedEntry, entryId, StringComparison.Ordinal))
                throw new InvalidDataException("Durable appraisal outbox entry ID mismatch.");
            return new DurableAppraisalOutboxEntry(
                entryId, requestFingerprint, packetId, ownerId, counterpartId, lineageId,
                saveId, worldId, storeSetId, generation, checkpointFingerprint, proposalTick,
                record, affectBefore, affectAfter, relationshipBefore, relationshipAfter,
                state, completed, reason, issued);
        }

        internal static string ComputeEntryId(
            string requestFingerprint,
            string checkpointFingerprint,
            string recordId,
            string affectAfter,
            string? relationshipAfter,
            string saveId,
            string worldId,
            string storeSetId) =>
            DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("schema", DurableAppraisalAdmissionSource.Contract),
                DurableAppraisalCanonical.Pair("request", requestFingerprint),
                DurableAppraisalCanonical.Pair("checkpoint_receipt_fingerprint", checkpointFingerprint),
                DurableAppraisalCanonical.Pair("record", recordId),
                DurableAppraisalCanonical.Pair("affect_after", affectAfter),
                DurableAppraisalCanonical.Pair("relationship_after", relationshipAfter ?? "null"),
                DurableAppraisalCanonical.Pair("save", saveId),
                DurableAppraisalCanonical.Pair("world", worldId),
                DurableAppraisalCanonical.Pair("store_set", storeSetId)
            });

        private static void WriteRecord(BinaryWriter writer, DevelopmentalAppraisalRecord record)
        {
            writer.Write(record.RecordId);
            writer.Write(record.PacketId);
            writer.Write(record.AppraisalId);
            writer.Write(record.OwnerId);
            writer.Write(record.LineageId);
            writer.Write(record.CounterpartId);
            writer.Write(record.SourceEventId);
            writer.Write(record.SourceEventHash);
            writer.Write(record.CheckpointGeneration);
            WriteAffectDelta(writer, record.RequestedAffectDelta);
            WriteAffectDelta(writer, record.ActualAffectDelta);
            WriteRelationshipDelta(writer, record.RequestedRelationshipDelta);
            WriteRelationshipDelta(writer, record.ActualRelationshipDelta);
            writer.Write(record.AffectBeforeFingerprint);
            writer.Write(record.AffectAfterFingerprint);
            WriteNullableString(writer, record.RelationshipBeforeFingerprint);
            WriteNullableString(writer, record.RelationshipAfterFingerprint);
        }

        private static DevelopmentalAppraisalRecord ReadRecord(BinaryReader reader) =>
            new DevelopmentalAppraisalRecord(
                reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString(),
                reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString(),
                reader.ReadInt64(), ReadAffectDelta(reader), ReadAffectDelta(reader),
                ReadRelationshipDelta(reader), ReadRelationshipDelta(reader),
                reader.ReadString(), reader.ReadString(), ReadNullableString(reader), ReadNullableString(reader));

        private static void WriteAffect(BinaryWriter writer, CanonicalAffectState state)
        {
            writer.Write(state.OwnerId);
            WriteAffectDelta(writer, state.Values);
            writer.Write(state.Version);
            writer.Write(state.Fingerprint);
        }

        private static CanonicalAffectState ReadAffect(BinaryReader reader) =>
            CanonicalAffectState.Restore(reader.ReadString(), ReadAffectDelta(reader), reader.ReadInt64(), reader.ReadString());

        private static void WriteRelationship(BinaryWriter writer, CanonicalRelationshipState? state)
        {
            writer.Write(state is not null);
            if (state is null) return;
            writer.Write(state.OwnerId);
            writer.Write(state.CounterpartId);
            writer.Write(state.Values.Trust);
            writer.Write(state.Values.Affection);
            writer.Write(state.Values.Fear);
            writer.Write(state.Values.Resentment);
            writer.Write(state.Version);
            writer.Write(state.Fingerprint);
        }

        private static CanonicalRelationshipState? ReadRelationship(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            var owner = reader.ReadString();
            var counterpart = reader.ReadString();
            var values = new CanonicalRelationshipVector(
                reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal());
            return CanonicalRelationshipState.Restore(owner, counterpart, values, reader.ReadInt64(), reader.ReadString());
        }

        private static void WriteAffectDelta(BinaryWriter writer, ProvisionalAffectDelta delta)
        {
            writer.Write(delta.Valence);
            writer.Write(delta.Arousal);
            writer.Write(delta.Threat);
            writer.Write(delta.Agency);
            writer.Write(delta.Attachment);
            writer.Write(delta.Certainty);
            writer.Write(delta.SocialStanding);
        }

        private static ProvisionalAffectDelta ReadAffectDelta(BinaryReader reader) =>
            new ProvisionalAffectDelta(
                reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal(),
                reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal());

        private static void WriteRelationshipDelta(BinaryWriter writer, ProvisionalRelationshipDelta delta)
        {
            writer.Write(delta.Trust);
            writer.Write(delta.Affection);
            writer.Write(delta.Fear);
            writer.Write(delta.Resentment);
        }

        private static ProvisionalRelationshipDelta ReadRelationshipDelta(BinaryReader reader) =>
            new ProvisionalRelationshipDelta(
                reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal(), reader.ReadDecimal());

        private static int BoundedCount(BinaryReader reader, string name, int maximum)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("Invalid " + name + " count.");
            return count;
        }

        private static void WriteNullableString(BinaryWriter writer, string? value)
        {
            writer.Write(value is not null);
            if (value is not null) writer.Write(value);
        }

        private static string? ReadNullableString(BinaryReader reader) =>
            reader.ReadBoolean() ? reader.ReadString() : null;

        private static void WriteNullableInt64(BinaryWriter writer, long? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue) writer.Write(value.Value);
        }

        private static long? ReadNullableInt64(BinaryReader reader) =>
            reader.ReadBoolean() ? reader.ReadInt64() : (long?)null;

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    public enum DurableAppraisalOutboxLoadStatus
    {
        LoadedPrimary = 0,
        RecoveredFromBackup = 1,
        NotFound = 2,
        Unrecoverable = 3
    }

    public sealed class DurableAppraisalOutboxLoadResult
    {
        internal DurableAppraisalOutboxLoadResult(
            DurableAppraisalOutboxLoadStatus status, DurableAppraisalOutbox? outbox, string diagnostic)
        {
            Status = status;
            Outbox = outbox;
            Diagnostic = diagnostic;
        }

        public DurableAppraisalOutboxLoadStatus Status { get; }
        public DurableAppraisalOutbox? Outbox { get; }
        public string Diagnostic { get; }
    }

    public sealed class AtomicDurableAppraisalOutboxStore
    {
        private readonly DurableAppraisalOutboxCodec _codec;

        public AtomicDurableAppraisalOutboxStore(DurableAppraisalOutboxCodec? codec = null)
        {
            _codec = codec ?? new DurableAppraisalOutboxCodec();
        }

        public void Save(string path, DurableCanonicalStoreSet stores, DurableAppraisalOutbox outbox)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Outbox directory is invalid.");
            Directory.CreateDirectory(directory);
            var temporary = fullPath + ".tmp";
            var backup = fullPath + ".bak";
            var bytes = _codec.Encode(stores, outbox);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            try
            {
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, backup, true);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public DurableAppraisalOutboxLoadResult Load(string path, DurableCanonicalStoreSet stores)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var backup = fullPath + ".bak";
            Exception? primaryFailure = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    return new DurableAppraisalOutboxLoadResult(
                        DurableAppraisalOutboxLoadStatus.LoadedPrimary,
                        _codec.Decode(File.ReadAllBytes(fullPath), stores),
                        "Primary durable appraisal outbox loaded and verified.");
                }
                catch (Exception exception) when (Recoverable(exception))
                {
                    primaryFailure = exception;
                }
            }
            if (File.Exists(backup))
            {
                try
                {
                    return new DurableAppraisalOutboxLoadResult(
                        DurableAppraisalOutboxLoadStatus.RecoveredFromBackup,
                        _codec.Decode(File.ReadAllBytes(backup), stores),
                        "Primary durable appraisal outbox invalid; verified backup loaded.");
                }
                catch (Exception exception) when (Recoverable(exception))
                {
                    return new DurableAppraisalOutboxLoadResult(
                        DurableAppraisalOutboxLoadStatus.Unrecoverable, null,
                        "Primary and backup durable appraisal outboxes are invalid. Primary: " +
                        (primaryFailure?.Message ?? "not found") + " Backup: " + exception.Message);
                }
            }
            return primaryFailure is null
                ? new DurableAppraisalOutboxLoadResult(
                    DurableAppraisalOutboxLoadStatus.NotFound, null, "No durable appraisal outbox exists.")
                : new DurableAppraisalOutboxLoadResult(
                    DurableAppraisalOutboxLoadStatus.Unrecoverable, null,
                    "Primary durable appraisal outbox is invalid and no backup exists: " + primaryFailure.Message);
        }

        private static bool Recoverable(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException ||
            exception is InvalidDataException || exception is FormatException ||
            exception is ArgumentException;
    }
}
