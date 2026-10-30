namespace ScatoloneDownloader
{
    /// <summary>
    /// Writes a file beside its target and moves it into place, so a crash, Ctrl-C or
    /// full disk never leaves a truncated file under the real name, which a
    /// skip-existing check would then accept as finished. On any failure the temp file
    /// is deleted (best effort) and the original exception is rethrown.
    /// </summary>
    internal static class AtomicFile
    {
        /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/>.</summary>
        internal static async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            string temp = path + ".tmp";

            try
            {
                await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>.</summary>
        internal static void WriteAllText(string path, string text)
        {
            string temp = path + ".tmp";

            try
            {
                File.WriteAllText(temp, text);
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        /// <summary>Copies <paramref name="sourceFile"/> to <paramref name="path"/>, replacing it.</summary>
        internal static void Copy(string sourceFile, string path)
        {
            string temp = path + ".tmp";

            try
            {
                File.Copy(sourceFile, temp, overwrite: true);
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private static void TryDelete(string temp)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A stray .tmp is harmless and overwritten on the next try.
            }
        }
    }
}
