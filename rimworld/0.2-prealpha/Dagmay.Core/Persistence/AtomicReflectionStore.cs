using System;
using System.IO;
using Dagmay.Core.Scheduling;

namespace Dagmay.Core.Persistence
{
    public enum ReflectionStoreLoadStatus
    {
        LoadedPrimary,
        RecoveredFromBackup,
        LoadedCheckpoint,
        NotFound,
        Unrecoverable
    }

    public sealed class ReflectionStoreLoadResult
    {
        public ReflectionStoreLoadResult(
            ReflectionStoreLoadStatus status,
            ReflectionStoreSnapshot? snapshot,
            string diagnostic)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ReflectionStoreLoadStatus Status { get; }
        public ReflectionStoreSnapshot? Snapshot { get; }
        public string Diagnostic { get; }
    }

    public sealed class AtomicReflectionStore
    {
        private readonly ReflectionStoreCodec _codec;

        public AtomicReflectionStore(ReflectionStoreCodec? codec = null)
        {
            _codec = codec ?? new ReflectionStoreCodec();
        }

        public void Save(string path, ReflectionStoreSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Reflection store path is required.", nameof(path));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Reflection store directory is invalid.");
            Directory.CreateDirectory(directory);

            var temporaryPath = fullPath + ".tmp";
            var backupPath = fullPath + ".bak";
            var bytes = _codec.Encode(snapshot);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            try
            {
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, backupPath, true);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public string PreserveCheckpoint(string path, ReflectionStoreSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var checkpointPath = GetCheckpointPath(path, snapshot.StoreId, snapshot.Generation);
            ImmutableCheckpointFile.Preserve(checkpointPath, _codec.Encode(snapshot));
            DecodeAndValidate(File.ReadAllBytes(checkpointPath), snapshot.StoreId, snapshot.Generation);
            return checkpointPath;
        }

        public string GetCheckpointPath(string path, Guid storeId, long generation)
        {
            return ImmutableCheckpointFile.GetPath(path, storeId, generation, ".reflection.checkpoint");
        }

        public bool HasValidCheckpoint(string path, Guid storeId, long generation)
        {
            try
            {
                var checkpointPath = GetCheckpointPath(path, storeId, generation);
                if (!File.Exists(checkpointPath)) return false;
                DecodeAndValidate(File.ReadAllBytes(checkpointPath), storeId, generation);
                return true;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return false;
            }
        }
        public long GetGenerationFloor(string path, Guid storeId)
        {
            if (storeId == Guid.Empty) throw new ArgumentException("Store ID cannot be empty.", nameof(storeId));
            var floor = 0L;
            var fullPath = Path.GetFullPath(path);
            floor = ReadGeneration(fullPath, storeId, floor);
            floor = ReadGeneration(fullPath + ".bak", storeId, floor);
            var checkpointDirectory = Path.GetDirectoryName(GetCheckpointPath(path, storeId, 0));
            if (checkpointDirectory is not null && Directory.Exists(checkpointDirectory))
            {
                foreach (var checkpoint in Directory.GetFiles(checkpointDirectory, "*.reflection.checkpoint"))
                {
                    floor = ReadGeneration(checkpoint, storeId, floor);
                    var fileName = Path.GetFileName(checkpoint);
                    var separator = fileName.IndexOf('.');
                    if (separator > 0
                        && long.TryParse(fileName.Substring(0, separator),
                            System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var addressedGeneration))
                        floor = Math.Max(floor, addressedGeneration);
                }
            }
            return floor;
        }
        public ReflectionStoreLoadResult Load(string path)
        {
            return LoadInternal(path, null, null);
        }

        public ReflectionStoreLoadResult Load(string path, Guid expectedStoreId, long expectedGeneration)
        {
            if (expectedStoreId == Guid.Empty)
                throw new ArgumentException("Expected store ID cannot be empty.", nameof(expectedStoreId));
            if (expectedGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedGeneration));
            return LoadInternal(path, expectedStoreId, expectedGeneration);
        }

        private ReflectionStoreLoadResult LoadInternal(
            string path,
            Guid? expectedStoreId,
            long? expectedGeneration)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Reflection store path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var backupPath = fullPath + ".bak";
            var checkpointPath = expectedStoreId.HasValue && expectedGeneration.HasValue
                ? GetCheckpointPath(fullPath, expectedStoreId.Value, expectedGeneration.Value)
                : null;
            Exception? primaryFailure = null;
            Exception? backupFailure = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.LoadedPrimary,
                        DecodeAndValidate(File.ReadAllBytes(fullPath), expectedStoreId, expectedGeneration),
                        "Primary reflection store loaded and checksum verified.");
                }
                catch (Exception exception) when (IsRecoverable(exception)) { primaryFailure = exception; }
            }
            if (File.Exists(backupPath))
            {
                try
                {
                    return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.RecoveredFromBackup,
                        DecodeAndValidate(File.ReadAllBytes(backupPath), expectedStoreId, expectedGeneration),
                        "Primary reflection store was invalid; verified backup loaded read-only.");
                }
                catch (Exception exception) when (IsRecoverable(exception)) { backupFailure = exception; }
            }
            if (checkpointPath is not null && File.Exists(checkpointPath))
            {
                try
                {
                    return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.LoadedCheckpoint,
                        DecodeAndValidate(File.ReadAllBytes(checkpointPath), expectedStoreId, expectedGeneration),
                        "Exact immutable reflection checkpoint loaded and checksum verified.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.Unrecoverable, null,
                        $"Primary, backup, and exact reflection checkpoint are invalid. Primary: {Message(primaryFailure)} Backup: {Message(backupFailure)} Checkpoint: {exception.Message}");
                }
            }
            if (backupFailure is not null)
                return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.Unrecoverable, null,
                    $"Primary and backup reflection stores are invalid and no exact checkpoint exists. Primary: {Message(primaryFailure)} Backup: {backupFailure.Message}");
            if (primaryFailure is not null)
                return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.Unrecoverable, null,
                    "Primary reflection store is invalid and no exact checkpoint exists: " + primaryFailure.Message);
            return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.NotFound, null, "No reflection store exists yet.");
        }
        private ReflectionStoreSnapshot DecodeAndValidate(
            byte[] encoded,
            Guid? expectedStoreId,
            long? expectedGeneration)
        {
            var snapshot = _codec.Decode(encoded);
            if (expectedStoreId.HasValue && snapshot.StoreId != expectedStoreId.Value)
            {
                throw new InvalidDataException("Reflection store ID does not match the save checkpoint.");
            }

            if (expectedGeneration.HasValue && snapshot.Generation != expectedGeneration.Value)
            {
                throw new InvalidDataException(
                    $"Reflection generation {snapshot.Generation} does not match save checkpoint generation {expectedGeneration.Value}.");
            }

            return snapshot;
        }

        private long ReadGeneration(string path, Guid storeId, long floor)
        {
            if (!File.Exists(path)) return floor;
            try
            {
                var snapshot = DecodeAndValidate(File.ReadAllBytes(path), storeId, null);
                return Math.Max(floor, snapshot.Generation);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return floor;
            }
        }
        private static bool IsRecoverable(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidDataException
                || exception is FormatException
                || exception is ArgumentException;
        }

        private static string Message(Exception? exception) => exception?.Message ?? "not found.";
    }
}
