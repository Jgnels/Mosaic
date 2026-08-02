using System;
using System.IO;

namespace Dagmay.Core.Persistence
{
    internal static class ImmutableCheckpointFile
    {
        public static string GetPath(string livePath, Guid storeId, long generation, string extension)
        {
            if (string.IsNullOrWhiteSpace(livePath)) throw new ArgumentException("Live path is required.", nameof(livePath));
            if (storeId == Guid.Empty) throw new ArgumentException("Store ID cannot be empty.", nameof(storeId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            var directory = Path.GetDirectoryName(Path.GetFullPath(livePath));
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Checkpoint directory is invalid.");
            return Path.Combine(directory, "Checkpoints", storeId.ToString("N"),
                generation.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + extension);
        }

        public static void Preserve(string path, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Checkpoint path is required.", nameof(path));
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Checkpoint directory is invalid.");
            Directory.CreateDirectory(directory);
            if (File.Exists(fullPath))
            {
                RequireExact(fullPath, bytes);
                return;
            }

            var temporaryPath = fullPath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }

                try { File.Move(temporaryPath, fullPath); }
                catch (IOException) when (File.Exists(fullPath)) { RequireExact(fullPath, bytes); }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static void RequireExact(string path, byte[] expected)
        {
            var actual = File.ReadAllBytes(path);
            if (actual.Length != expected.Length) throw new InvalidDataException("An immutable checkpoint already exists with conflicting bytes.");
            var difference = 0;
            for (var index = 0; index < actual.Length; index++) difference |= actual[index] ^ expected[index];
            if (difference != 0) throw new InvalidDataException("An immutable checkpoint already exists with conflicting bytes.");
        }
    }
}
