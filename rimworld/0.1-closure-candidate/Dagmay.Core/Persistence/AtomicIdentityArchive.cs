using System;
using System.IO;

namespace Dagmay.Core.Persistence
{
    public enum ArchiveLoadStatus
    {
        LoadedPrimary,
        RecoveredFromBackup,
        NotFound,
        Unrecoverable
    }

    public sealed class ArchiveLoadResult
    {
        public ArchiveLoadResult(ArchiveLoadStatus status, IdentityArchiveSnapshot? snapshot, string diagnostic)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ArchiveLoadStatus Status { get; }
        public IdentityArchiveSnapshot? Snapshot { get; }
        public string Diagnostic { get; }
    }

    public sealed class AtomicIdentityArchive
    {
        private readonly IdentityArchiveCodec _codec;

        public AtomicIdentityArchive(IdentityArchiveCodec? codec = null)
        {
            _codec = codec ?? new IdentityArchiveCodec();
        }

        public void Save(string path, IdentityArchiveSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Archive path is required.", nameof(path));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Archive directory is invalid.");
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
                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public ArchiveLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Archive path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var backupPath = fullPath + ".bak";
            Exception? primaryFailure = null;

            if (File.Exists(fullPath))
            {
                try
                {
                    return new ArchiveLoadResult(
                        ArchiveLoadStatus.LoadedPrimary,
                        _codec.Decode(File.ReadAllBytes(fullPath)),
                        "Primary identity archive loaded and checksum verified.");
                }
                catch (Exception exception) when (IsRecoverableReadFailure(exception))
                {
                    primaryFailure = exception;
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    return new ArchiveLoadResult(
                        ArchiveLoadStatus.RecoveredFromBackup,
                        _codec.Decode(File.ReadAllBytes(backupPath)),
                        "Primary identity archive was unavailable or invalid; verified backup loaded read-only for recovery.");
                }
                catch (Exception exception) when (IsRecoverableReadFailure(exception))
                {
                    return new ArchiveLoadResult(
                        ArchiveLoadStatus.Unrecoverable,
                        null,
                        $"Primary and backup identity archives are invalid. Primary: {Message(primaryFailure)} Backup: {exception.Message}");
                }
            }

            if (primaryFailure is not null)
            {
                return new ArchiveLoadResult(
                    ArchiveLoadStatus.Unrecoverable,
                    null,
                    $"Primary identity archive is invalid and no backup exists: {primaryFailure.Message}");
            }

            return new ArchiveLoadResult(ArchiveLoadStatus.NotFound, null, "No identity archive exists yet.");
        }

        private static bool IsRecoverableReadFailure(Exception exception)
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
