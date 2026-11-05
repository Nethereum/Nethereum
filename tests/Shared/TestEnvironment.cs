using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Nethereum.Testing
{
    public static class TestEnvironment
    {
        public const string SettingsRelativePath = "tests/Nethereum.XUnitEthereumClients/appsettings.test.json";
        public const string LocalSettingsRelativePath = "tests/Nethereum.XUnitEthereumClients/appsettings.test.local.json";

        private static readonly Lazy<string> RepoRootLazy = new Lazy<string>(FindRepoRoot);
        private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> SectionsLazy =
            new Lazy<Dictionary<string, Dictionary<string, string>>>(LoadSections);

        public static string RepoRoot => RepoRootLazy.Value;

        public static string ExternalPath(string key) => ResolvePath(Setting("ExternalPaths", key));

        public static bool HasExternalPath(string key)
        {
            var value = SettingOrNull("ExternalPaths", key);
            if (string.IsNullOrEmpty(value)) return false;
            var path = ResolvePath(value);
            return Directory.Exists(path) || File.Exists(path);
        }

        public static string RequireExternalPath(string key)
        {
            var path = ExternalPath(key);
            if (!Directory.Exists(path) && !File.Exists(path))
                throw new InvalidOperationException(
                    $"External test dependency 'ExternalPaths:{key}' is not present at '{path}'. " +
                    $"Run `dotnet run ci/fetch-externals.cs`, or set ExternalPaths:{key} in {LocalSettingsRelativePath}.");
            return path;
        }

        public static string Tool(string key)
        {
            var value = Setting("Tools", key);
            if (!LooksLikePath(value)) return value;
            var path = ResolvePath(value);
            if (OperatingSystem.IsWindows() && !File.Exists(path) && File.Exists(path + ".exe")) return path + ".exe";
            return path;
        }

        public static string Service(string key) => Setting("Services", key);

        public static string ServiceOrNull(string key) => SettingOrNull("Services", key);

        public static string Setting(string section, string key)
        {
            var value = SettingOrNull(section, key);
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException(
                    $"Test setting '{section}:{key}' is not set. Add it to {LocalSettingsRelativePath}.");
            return value;
        }

        public static string SettingOrNull(string section, string key) =>
            SectionsLazy.Value.TryGetValue(section, out var values) && values.TryGetValue(key, out var value)
                ? value
                : null;

        private static bool LooksLikePath(string value) =>
            value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0;

        public const string ExternalsRootVariable = "NETHEREUM_EXTERNALS";

        private const string ExternalsFolder = "external";

        private static string ResolvePath(string value)
        {
            var normalised = value.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalised)) return Path.GetFullPath(normalised);

            var externalsPrefix = ExternalsFolder + Path.DirectorySeparatorChar;
            if (normalised.StartsWith(externalsPrefix, StringComparison.Ordinal))
                return ResolveExternal(normalised.Substring(externalsPrefix.Length));

            return Path.GetFullPath(Path.Combine(RepoRoot, normalised));
        }

        private static string ResolveExternal(string relative)
        {
            var overrideRoot = Environment.GetEnvironmentVariable(ExternalsRootVariable);
            if (!string.IsNullOrEmpty(overrideRoot))
                return Path.GetFullPath(Path.Combine(overrideRoot, relative));

            var local = Path.GetFullPath(Path.Combine(RepoRoot, ExternalsFolder, relative));
            if (Directory.Exists(local) || File.Exists(local)) return local;

            var mainCheckout = FindMainCheckout(RepoRoot);
            if (mainCheckout != null)
            {
                var shared = Path.GetFullPath(Path.Combine(mainCheckout, ExternalsFolder, relative));
                if (Directory.Exists(shared) || File.Exists(shared)) return shared;
            }
            return local;
        }

        private static string FindMainCheckout(string repoRoot)
        {
            var dotGit = Path.Combine(repoRoot, ".git");
            if (!File.Exists(dotGit)) return null;
            foreach (var line in File.ReadLines(dotGit))
            {
                const string prefix = "gitdir:";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var gitDir = Path.GetFullPath(Path.Combine(repoRoot, line.Substring(prefix.Length).Trim()));
                var worktrees = Directory.GetParent(gitDir);
                var commonDir = worktrees?.Parent;
                if (worktrees == null || commonDir == null || worktrees.Name != "worktrees" || commonDir.Name != ".git") return null;
                return commonDir.Parent?.FullName;
            }
            return null;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                $"Could not find the repository root (Nethereum.slnx) above '{AppContext.BaseDirectory}'.");
        }

        private static Dictionary<string, Dictionary<string, string>> LoadSections()
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Merge(sections, Path.Combine(RepoRoot, SettingsRelativePath));
            Merge(sections, Path.Combine(RepoRoot, LocalSettingsRelativePath));
            return sections;
        }

        private static void Merge(Dictionary<string, Dictionary<string, string>> sections, string file)
        {
            if (!File.Exists(file)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(file),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var section in document.RootElement.EnumerateObject())
            {
                if (section.Value.ValueKind != JsonValueKind.Object) continue;
                if (!sections.TryGetValue(section.Name, out var values))
                {
                    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[section.Name] = values;
                }
                foreach (var entry in section.Value.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String)
                        values[entry.Name] = entry.Value.GetString();
                    else if (entry.Value.ValueKind != JsonValueKind.Null)
                        values[entry.Name] = entry.Value.GetRawText();
                }
            }
        }
    }
}
