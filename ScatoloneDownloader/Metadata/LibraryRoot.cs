using System.Text.Json;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// Finds the one folder the cube commands work against: the library root,
    /// holding <c>Source\</c> (the images) and <c>metadata\</c> (the store). The root comes from <c>--root</c> or, by
    /// default, from the <c>"root"</c> property of <c>scatolone.json</c> beside
    /// the executable, so the same path is never typed per command. Pure apart
    /// from reading that one file, and the settings folder is a parameter, so
    /// tests never depend on the real settings file.
    /// </summary>
    internal static class LibraryRoot
    {
        /// <summary>Name of the settings file, looked up in the settings directory.</summary>
        internal const string SettingsFileName = "scatolone.json";

        /// <summary>
        /// Resolves the library root as an absolute path. A non-blank
        /// <paramref name="cliRoot"/> wins; otherwise the <c>root</c> of the
        /// settings file in <paramref name="settingsDirectory"/> is used (a
        /// relative value resolves against that folder). Returns <c>null</c> with
        /// a one-line <paramref name="error"/> that names what to do when there is
        /// no usable root or the folder does not exist.
        /// </summary>
        internal static string? Resolve(string? cliRoot, string settingsDirectory, out string? error)
        {
            error = null;
            string root;

            if (!string.IsNullOrWhiteSpace(cliRoot))
            {
                root = Path.GetFullPath(cliRoot);
            }
            else
            {
                string settingsPath = Path.GetFullPath(Path.Combine(settingsDirectory, SettingsFileName));

                if (!File.Exists(settingsPath))
                {
                    error = $"No library root: pass --root <DIR> or set \"root\" in {settingsPath}.";
                    return null;
                }

                string? configured = ReadRoot(settingsPath, out error);
                if (configured is null)
                {
                    return null;
                }

                root = Path.GetFullPath(configured, Path.GetFullPath(settingsDirectory));
            }

            if (!Directory.Exists(root))
            {
                error = $"Library root not found: {root}";
                return null;
            }

            return root;
        }

        /// <summary>The folder of the master images, <c>&lt;root&gt;\Source</c>.</summary>
        internal static string Source(string root) => Path.Combine(root, "Source");

        /// <summary>The git-tracked metadata store, <c>&lt;root&gt;\metadata</c>.</summary>
        internal static string Metadata(string root) => Path.Combine(root, "metadata");

        private static string? ReadRoot(string settingsPath, out string? error)
        {
            error = null;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "root", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        {
                            return property.Value.GetString();
                        }

                        error = $"The \"root\" in {settingsPath} is blank: set it to the library folder or pass --root <DIR>.";
                        return null;
                    }
                }

                error = $"No \"root\" in {settingsPath}: add it or pass --root <DIR>.";
                return null;
            }
            catch (JsonException ex)
            {
                error = $"Malformed JSON in {settingsPath} ({ex.Message}): fix it or pass --root <DIR>.";
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"Cannot read {settingsPath} ({ex.Message}): fix it or pass --root <DIR>.";
                return null;
            }
        }
    }
}
