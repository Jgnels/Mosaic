using System;
using System.IO;
using Dagmay.Core.Scheduling;

namespace Dagmay.Core.Persistence
{
    public enum ReflectionStoreLoadStatus
    {
        LoadedPrimary,
        RecoveredFromBackup,
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

        public ReflectionStoreLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Reflection store path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var backupPath = fullPath + ".bak";
            Exception? primaryFailure = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    return new ReflectionStoreLoadResult(
                        ReflectionStoreLoadStatus.LoadedPrimary,
                        _codec.Decode(File.ReadAllBytes(fullPath)),
                        "Primary reflection store loaded and checksum verified.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    primaryFailure = exception;
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    return new ReflectionStoreLoadResult(
                        ReflectionStoreLoadStatus.RecoveredFromBackup,
                        _codec.Decode(File.ReadAllBytes(backupPath)),
                        "Primary reflection store was invalid; verified backup loaded read-only.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    return new ReflectionStoreLoadResult(
                        ReflectionStoreLoadStatus.Unrecoverable,
                        null,
                        $"Primary and backup reflection stores are invalid. Primary: {Message(primaryFailure)} Backup: {exception.Message}");
                }
            }

            if (primaryFailure is not null)
            {
                return new ReflectionStoreLoadResult(
                    ReflectionStoreLoadStatus.Unrecoverable,
                    null,
                    "Primary reflection store is invalid and no backup exists: " + primaryFailure.Message);
            }

            return new ReflectionStoreLoadResult(ReflectionStoreLoadStatus.NotFound, null, "No reflection store exists yet.");
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
