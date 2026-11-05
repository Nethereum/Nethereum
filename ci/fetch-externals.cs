#:property PublishAot=false

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var isWindows = OperatingSystem.IsWindows();
var root = FindRoot();
var manifestPath = Path.Combine(root, "ci", "externals.json");
var externalsRoot = ResolveExternalsRoot(root);

var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
var groups = manifest["groups"]!.AsObject();
var externals = new Dictionary<string, JsonObject>();
foreach (var node in manifest["externals"]!.AsArray())
    externals[(string)node!["name"]!] = node.AsObject();
var sources = new Dictionary<string, JsonObject>();
foreach (var entry in externals.Values)
    if (entry.ContainsKey("dest")) sources[(string)entry["dest"]!] = entry;
foreach (var name in externals.Keys.ToList())
{
    var entry = externals[name];
    if ((string)entry["kind"]! != "go-build") continue;
    var builtFrom = entry.ContainsKey("depends") ? externals[(string)entry["depends"]!] : null;
    if (builtFrom == null && sources.TryGetValue((string)entry["source"]!, out var sourceEntry)) builtFrom = sourceEntry;
    var inRepoSource = !sources.ContainsKey((string)entry["source"]!);
    if (builtFrom == null && !inRepoSource) continue;
    var augmented = (JsonObject)entry.DeepClone();
    if (builtFrom != null) augmented["built_from"] = builtFrom.DeepClone();
    if (inRepoSource) augmented["source_sha256"] = GoSourceHash(Path.Combine(root, (string)entry["source"]!));
    externals[name] = augmented;
}

var names = new List<string>();
var force = false;
var list = false;
foreach (var arg in args)
{
    if (arg == "--force") force = true;
    else if (arg == "--list") list = true;
    else if (arg == "-h" || arg == "--help")
    {
        Console.WriteLine("usage: dotnet run ci/fetch-externals.cs -- [names...] [--force] [--list]");
        Console.WriteLine("Provision the external test dependencies listed in ci/externals.json.");
        Console.WriteLine("names: externals or groups to fetch (" + string.Join(", ", groups.Select(g => g.Key).OrderBy(k => k, StringComparer.Ordinal)) + "); default: all");
        Console.WriteLine("--force  re-fetch even when the stamp says it is current");
        Console.WriteLine("--list   list externals and whether each is current");
        return 0;
    }
    else if (arg.StartsWith("--")) { Console.Error.WriteLine($"unrecognized argument {arg}"); return 2; }
    else names.Add(arg);
}

try
{
    if (list)
    {
        foreach (var (name, entry) in externals)
            Log($"{(IsCurrent(entry) ? "current" : "missing")}  {name}");
        return 0;
    }

    var selected = new List<string>();
    foreach (var name in names.Count > 0 ? names : externals.Keys.ToList())
    {
        var members = groups.TryGetPropertyValue(name, out var group)
            ? group!.AsArray().Select(n => (string)n!).ToList()
            : new List<string> { name };
        foreach (var item in members)
        {
            if (!externals.ContainsKey(item)) throw new FetchException($"unknown external or group '{item}'");
            if (!selected.Contains(item)) selected.Add(item);
        }
    }

    foreach (var name in selected)
    {
        var entry = externals[name];
        if (IsCurrent(entry) && !force)
        {
            Log($"[current] {name}");
            continue;
        }
        Log($"[fetch] {name}");
        switch ((string)entry["kind"]!)
        {
            case "git": FetchGit(entry); break;
            case "archive": FetchArchive(entry); break;
            case "binaries": FetchBinaries(entry); break;
            case "go-build": FetchGoBuild(entry); break;
            case "npm": FetchNpm(entry); break;
            default: throw new FetchException($"unknown kind '{entry["kind"]}' for {name}");
        }
        WriteStamp(entry);
    }
    Log("done");
    return 0;
}
catch (FetchException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

string FindRoot()
{
    var script = AppContext.GetData("EntryPointFilePath") as string;
    var start = script != null ? Path.GetDirectoryName(Path.GetFullPath(script))! : Directory.GetCurrentDirectory();
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "ci", "externals.json"))) return dir.FullName;
    throw new InvalidOperationException("ci/externals.json not found above " + start);
}

string ResolveExternalsRoot(string repoRoot)
{
    var overrideRoot = Environment.GetEnvironmentVariable("NETHEREUM_EXTERNALS");
    if (!string.IsNullOrEmpty(overrideRoot)) return Path.GetFullPath(overrideRoot);
    var main = FindMainCheckout(repoRoot);
    return Path.Combine(main != null && Directory.Exists(main) ? main : repoRoot, "external");
}

string? FindMainCheckout(string repoRoot)
{
    var dotGit = Path.Combine(repoRoot, ".git");
    if (!File.Exists(dotGit)) return null;
    foreach (var line in File.ReadLines(dotGit))
    {
        const string prefix = "gitdir:";
        if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
        var gitDir = Path.GetFullPath(Path.Combine(repoRoot, line[prefix.Length..].Trim()));
        var worktrees = Directory.GetParent(gitDir);
        var commonDir = worktrees?.Parent;
        if (worktrees == null || commonDir == null || worktrees.Name != "worktrees" || commonDir.Name != ".git") return null;
        return commonDir.Parent?.FullName;
    }
    return null;
}

string LocalPath(string path)
{
    var parts = path.Replace('\\', '/').Split('/', 2);
    if (parts[0] == "external" && parts.Length == 2)
        return Path.Combine(externalsRoot, parts[1].Replace('/', Path.DirectorySeparatorChar));
    return Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
}

string LongPath(string path)
{
    path = Path.GetFullPath(path);
    return isWindows && !path.StartsWith(@"\\?\", StringComparison.Ordinal) ? @"\\?\" + path : path;
}

void Log(string message)
{
    Console.WriteLine(message);
    Console.Out.Flush();
}

void Run(List<string> cmd, string? cwd = null)
{
    if (cmd[0] == "git")
    {
        var options = new List<string> { "-c", "url.https://github.com/.insteadOf=git@github.com:" };
        if (isWindows) options.AddRange(new[] { "-c", "core.longpaths=true" });
        cmd = new List<string> { "git" }.Concat(options).Concat(cmd.Skip(1)).ToList();
    }
    Log("  $ " + string.Join(" ", cmd));
    var shell = isWindows && (cmd[0] == "yarn" || cmd[0] == "npm" || cmd[0] == "npx");
    var info = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = cwd ?? Directory.GetCurrentDirectory() };
    if (shell)
    {
        info.FileName = "cmd.exe";
        info.ArgumentList.Add("/c");
        foreach (var part in cmd) info.ArgumentList.Add(part);
    }
    else
    {
        info.FileName = cmd[0];
        foreach (var part in cmd.Skip(1)) info.ArgumentList.Add(part);
    }
    Process process;
    try { process = Process.Start(info)!; }
    catch (System.ComponentModel.Win32Exception ex) { throw new FetchException($"cannot start {cmd[0]}: {ex.Message}"); }
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new FetchException($"command '{string.Join(" ", cmd)}' returned non-zero exit status {process.ExitCode}");
}

string StampPath(JsonObject entry) => LocalPath("external/.stamps/" + (string)entry["name"]! + ".json");

List<string> InstalledPaths(JsonObject entry)
{
    var suffix = isWindows ? ".exe" : "";
    switch ((string)entry["kind"]!)
    {
        case "git":
        case "archive":
            return new List<string> { (string)entry["dest"]! };
        case "binaries":
            return entry["install"]!.AsObject().Select(kv => (string)kv.Value! + suffix).ToList();
        case "go-build":
            return new List<string> { (string)entry["output"]! + suffix };
        case "npm":
            return new List<string> { ((string)entry["dir"]!).TrimEnd('/') + "/node_modules" };
        default:
            return new List<string>();
    }
}

bool IsCurrent(JsonObject entry)
{
    var path = StampPath(entry);
    if (!File.Exists(path)) return false;
    if (!InstalledPaths(entry).All(p => File.Exists(LocalPath(p)) || Directory.Exists(LocalPath(p)))) return false;
    return JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), entry);
}

JsonNode SortKeys(JsonNode? node)
{
    switch (node)
    {
        case JsonObject obj:
            var sorted = new JsonObject();
            foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal)) sorted[kv.Key] = SortKeys(kv.Value);
            return sorted;
        case JsonArray array:
            var copy = new JsonArray();
            foreach (var item in array) copy.Add(SortKeys(item));
            return copy;
        default:
            return node!.DeepClone();
    }
}

void WriteStamp(JsonObject entry)
{
    var path = StampPath(entry);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var options = new JsonSerializerOptions { WriteIndented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    File.WriteAllText(path, SortKeys(entry).ToJsonString(options), new UTF8Encoding(false));
}

string Download(string url, string sha256)
{
    var path = Path.Combine(Path.GetTempPath(), "nethereum-external-" + Guid.NewGuid().ToString("N"));
    Log($"  downloading {url}");
    using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
    using var sha = SHA256.Create();
    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
    {
        response.EnsureSuccessStatusCode();
        using var input = response.Content.ReadAsStream();
        using var output = File.Create(path);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            output.Write(buffer, 0, read);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    }
    var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    if (actual != sha256)
    {
        File.Delete(path);
        throw new FetchException($"sha256 mismatch for {url}: expected {sha256}, got {actual}");
    }
    return path;
}

Stream OpenTar(string path)
{
    var file = File.OpenRead(path);
    var magic = new byte[6];
    var count = file.Read(magic, 0, magic.Length);
    file.Position = 0;
    if (count >= 2 && magic[0] == 0x1f && magic[1] == 0x8b) return new GZipStream(file, CompressionMode.Decompress);
    if (count >= 6 && magic[0] == 0xfd && magic[1] == 0x37 && magic[2] == 0x7a && magic[3] == 0x58 && magic[4] == 0x5a)
        throw new FetchException("xz archives are not supported");
    if (count >= 3 && magic[0] == 0x42 && magic[1] == 0x5a && magic[2] == 0x68)
        throw new FetchException("bzip2 archives are not supported");
    return file;
}

bool FnMatch(string name, string pattern)
{
    var regex = "^" + string.Concat(pattern.Select(c => c == '*' ? ".*" : c == '?' ? "." : Regex.Escape(c.ToString()))) + "$";
    return Regex.IsMatch(name, regex, RegexOptions.Singleline);
}

string? ArchiveTarget(string name, List<string>? keep, string? lift)
{
    if (keep != null && keep.Count > 0 && !keep.Any(p => FnMatch(name, p + "*"))) return null;
    if (!string.IsNullOrEmpty(lift))
    {
        var index = name.IndexOf(lift, StringComparison.Ordinal);
        if (index >= 0) name = name[..index] + name[(index + lift.Length)..];
    }
    return name;
}

void FetchArchive(JsonObject entry)
{
    var dest = LongPath(LocalPath((string)entry["dest"]!));
    var path = Download((string)entry["url"]!, (string)entry["sha256"]!);
    try
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        Directory.CreateDirectory(dest);
        var keep = entry["keep"]?.AsArray().Select(n => (string)n!).ToList();
        var lift = (string?)entry["lift"];
        var fullDest = Path.GetFullPath(dest);
        using var stream = OpenTar(path);
        using var reader = new TarReader(stream);
        TarEntry? member;
        while ((member = reader.GetNextEntry()) != null)
        {
            if (member.EntryType != TarEntryType.RegularFile && member.EntryType != TarEntryType.V7RegularFile) continue;
            var target = ArchiveTarget(member.Name, keep, lift);
            if (target == null) continue;
            var normalised = Path.GetFullPath(Path.Combine(dest, target.Replace('/', Path.DirectorySeparatorChar)));
            if (!normalised.StartsWith(fullDest, StringComparison.Ordinal))
                throw new FetchException($"refusing to extract {member.Name} outside {dest}");
            Directory.CreateDirectory(Path.GetDirectoryName(normalised)!);
            using var output = File.Create(normalised);
            member.DataStream?.CopyTo(output);
        }
    }
    finally
    {
        File.Delete(path);
    }
}

void FetchBinaries(JsonObject entry)
{
    var platformEntry = entry[isWindows ? "windows" : "linux"]!.AsObject();
    var path = Download((string)platformEntry["url"]!, (string)platformEntry["sha256"]!);
    try
    {
        var suffix = isWindows ? ".exe" : "";
        var wanted = entry["install"]!.AsObject().ToDictionary(kv => kv.Key + suffix, kv => LocalPath((string)kv.Value!) + suffix);
        var found = new HashSet<string>();
        if (isWindows)
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var item in archive.Entries)
            {
                var baseName = Path.GetFileName(item.FullName);
                if (!wanted.TryGetValue(baseName, out var target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                item.ExtractToFile(target, true);
                found.Add(baseName);
            }
        }
        else
        {
            using var stream = OpenTar(path);
            using var reader = new TarReader(stream);
            TarEntry? member;
            while ((member = reader.GetNextEntry()) != null)
            {
                var baseName = Path.GetFileName(member.Name);
                if ((member.EntryType != TarEntryType.RegularFile && member.EntryType != TarEntryType.V7RegularFile) || !wanted.TryGetValue(baseName, out var target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var output = File.Create(target)) member.DataStream?.CopyTo(output);
                File.SetUnixFileMode(target, (UnixFileMode)Convert.ToInt32("755", 8));
                found.Add(baseName);
            }
        }
        var missing = wanted.Keys.Where(k => !found.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            throw new FetchException($"{(string)entry["name"]!}: archive did not contain [{string.Join(", ", missing.Select(m => "'" + m + "'"))}]");
    }
    finally
    {
        File.Delete(path);
    }
}

void FetchGit(JsonObject entry)
{
    var dest = LocalPath((string)entry["dest"]!);
    if (!Directory.Exists(Path.Combine(dest, ".git")))
    {
        Directory.CreateDirectory(dest);
        Run(new List<string> { "git", "init", "-q" }, dest);
        Run(new List<string> { "git", "remote", "add", "origin", (string)entry["repo"]! }, dest);
    }
    Run(new List<string> { "git", "fetch", "-q", "--depth", "1", "origin", (string)entry["commit"]! }, dest);
    Run(new List<string> { "git", "checkout", "-q", "--force", "FETCH_HEAD" }, dest);
    Run(new List<string> { "git", "clean", "-q", "-fdx", "-e", "node_modules", "-e", ".venv" }, dest);
    if (entry.ContainsKey("submodules"))
    {
        Run(new List<string> { "git", "submodule", "update", "-q", "--init", "--recursive", "--force", "--depth", "1" }, dest);
        foreach (var kv in entry["submodules"]!.AsObject())
        {
            var sub = Path.Combine(dest, kv.Key.Replace('/', Path.DirectorySeparatorChar));
            Run(new List<string> { "git", "fetch", "-q", "--depth", "1", "origin", (string)kv.Value! }, sub);
            Run(new List<string> { "git", "checkout", "-q", "--force", (string)kv.Value! }, sub);
        }
    }
    if (entry.ContainsKey("patch"))
        Run(new List<string> { "git", "apply", Path.Combine(root, (string)entry["patch"]!) }, dest);
    if (entry["post"] != null)
        foreach (var node in entry["post"]!.AsArray())
        {
            var cmd = node!.AsArray().Select(n => (string)n!).ToList();
            if (cmd[0].StartsWith(".venv/", StringComparison.Ordinal))
                cmd[0] = Path.Combine(dest, ".venv", isWindows ? "Scripts" : "bin", Path.GetFileName(cmd[0]));
            Run(cmd, dest);
        }
}

void FetchGoBuild(JsonObject entry)
{
    var output = LocalPath((string)entry["output"]!) + (isWindows ? ".exe" : "");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var sourceDir = LocalPath((string)entry["source"]!);
    var build = new List<string> { "go", "build", "-o", output };
    if (entry.ContainsKey("replace"))
    {
        var modDir = Path.Combine(Path.GetTempPath(), "nethereum-gomod-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modDir);
        var modFile = Path.Combine(modDir, "go.mod");
        File.Copy(Path.Combine(sourceDir, "go.mod"), modFile);
        File.Copy(Path.Combine(sourceDir, "go.sum"), Path.Combine(modDir, "go.sum"));
        var target = LocalPath((string)externals[(string)entry["depends"]!]["dest"]!);
        Run(new List<string> { "go", "mod", "edit", "-modfile=" + modFile, "-replace", (string)entry["replace"]! + "=" + target }, sourceDir);
        build.Add("-modfile=" + modFile);
    }
    build.Add((string)entry["package"]!);
    Run(build, sourceDir);
}

string GoSourceHash(string directory)
{
    using var sha = SHA256.Create();
    var files = Directory.GetFiles(directory)
        .Where(f => f.EndsWith(".go", StringComparison.Ordinal) || Path.GetFileName(f) is "go.mod" or "go.sum")
        .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);
    foreach (var file in files)
    {
        var nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(file) + "\n");
        sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
        var content = File.ReadAllText(file).Replace("\r\n", "\n");
        var contentBytes = Encoding.UTF8.GetBytes(content);
        sha.TransformBlock(contentBytes, 0, contentBytes.Length, null, 0);
    }
    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
}

void FetchNpm(JsonObject entry)
{
    Run(new List<string> { "npm", "ci", "--no-audit", "--no-fund" }, Path.Combine(root, (string)entry["dir"]!));
}

class FetchException : Exception
{
    public FetchException(string message) : base(message) { }
}
