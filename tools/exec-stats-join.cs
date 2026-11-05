#:property PublishAot=false

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

var arguments = ParseArguments(args);
if (!arguments.TryGetValue("geth", out var gethPath) || !arguments.TryGetValue("ours", out var oursPath))
{
    Console.Error.WriteLine("usage: dotnet run tools/exec-stats-join.cs -- --geth <geth.log> --ours <node.log> [--top 20]");
    return 2;
}
var top = arguments.TryGetValue("top", out var topText) ? int.Parse(topText, CultureInfo.InvariantCulture) : 20;

var geth = ReadGeth(gethPath);
var ours = ReadOurs(oursPath);
var joined = ours.Keys.Where(geth.ContainsKey).OrderBy(b => b).ToList();

Console.WriteLine($"geth blocks: {geth.Count}  our blocks: {ours.Count}  joined: {joined.Count}");
if (joined.Count == 0) return 1;

var rows = new List<(string Field, Func<Block, double> Ours, Func<Block, double>? Geth)>
{
    ("total_ms", o => o.Ms("total_ms"), g => g.Value("total_ms")),
    ("execution_ms", o => o.Ms("execution_ms"), g => g.Value("execution_ms")),
    ("state_read_ms", o => o.Ms("account_reads_ms") + o.Ms("storage_reads_ms") + o.Ms("code_reads_ms"), g => g.Value("state_read_ms")),
    ("state_hash_ms", o => o.Ms("account_hashes_ms") + o.Ms("account_updates_ms") + o.Ms("storage_updates_ms"), g => g.Value("state_hash_ms")),
    ("commit_ms", o => o.Ms("account_commits_ms") + o.Ms("storage_commits_ms") + o.Ms("db_commit_ms") + o.Ms("block_write_ms"), g => g.Value("commit_ms")),
    ("mgasps", o => o.Ms("mgasps"), g => g.Value("mgas_per_sec")),
    ("accounts_loaded", o => o.Ms("accounts_loaded"), g => g.Value("accounts_loaded")),
    ("storage_loaded", o => o.Ms("storage_loaded"), g => g.Value("storage_loaded")),
    ("code_loaded", o => o.Ms("code_loaded"), g => g.Value("code_loaded")),
    ("code_load_bytes", o => o.Ms("code_load_bytes"), g => g.Value("code_load_bytes")),
    ("accounts_updated", o => o.Ms("accounts_updated"), g => g.Value("accounts_updated")),
    ("accounts_deleted", o => o.Ms("accounts_deleted"), g => g.Value("accounts_deleted")),
    ("storage_updated", o => o.Ms("storage_updated"), g => g.Value("storage_updated")),
    ("storage_deleted", o => o.Ms("storage_deleted"), g => g.Value("storage_deleted")),
    ("code_updated", o => o.Ms("code_updated"), g => g.Value("code_updated")),
    ("code_update_bytes", o => o.Ms("code_update_bytes"), g => g.Value("code_update_bytes")),
};
var oursOnly = new[]
{
    "validation_ms", "account_reads_ms", "storage_reads_ms", "code_reads_ms", "account_updates_ms", "account_hashes_ms",
    "account_commits_ms", "storage_updates_ms", "storage_commits_ms", "db_commit_ms", "block_write_ms",
};

Console.WriteLine();
Console.WriteLine($"{"field",-20} {"ours p50",12} {"ours p90",12} {"geth p50",12} {"geth p90",12} {"p50 ratio",10}");
foreach (var (field, oursOf, gethOf) in rows)
{
    var o = joined.Select(b => oursOf(ours[b])).ToList();
    var g = joined.Select(b => gethOf!(geth[b])).ToList();
    var ratio = Percentile(g, 50) > 0 ? Percentile(o, 50) / Percentile(g, 50) : double.NaN;
    Console.WriteLine($"{field,-20} {Percentile(o, 50),12:F3} {Percentile(o, 90),12:F3} {Percentile(g, 50),12:F3} {Percentile(g, 90),12:F3} {ratio,10:F2}");
}
Console.WriteLine();
Console.WriteLine("ours-only fields (geth logSlow does not emit these individually)");
foreach (var field in oursOnly)
{
    var o = joined.Select(b => ours[b].Ms(field)).ToList();
    Console.WriteLine($"{field,-20} {Percentile(o, 50),12:F3} {Percentile(o, 90),12:F3}");
}

Console.WriteLine();
Console.WriteLine($"worst {top} blocks by (ours total_ms - geth total_ms)");
Console.WriteLine($"{"block",10} {"txs",5} {"gas",10} {"ours total",11} {"geth total",11} {"delta",10} {"ours exec",10} {"geth exec",10} {"ours read",10} {"geth read",10} {"ours hash",10} {"geth hash",10} {"ours cmt",10} {"geth cmt",10}");
foreach (var b in joined.OrderByDescending(b => ours[b].Ms("total_ms") - geth[b].Value("total_ms")).Take(top))
{
    var o = ours[b];
    var g = geth[b];
    Console.WriteLine(
        $"{b,10} {o.Ms("txs"),5:F0} {o.Ms("gas"),10:F0} {o.Ms("total_ms"),11:F2} {g.Value("total_ms"),11:F2} {o.Ms("total_ms") - g.Value("total_ms"),10:F2} " +
        $"{rows[1].Ours(o),10:F2} {g.Value("execution_ms"),10:F2} {rows[2].Ours(o),10:F2} {g.Value("state_read_ms"),10:F2} " +
        $"{rows[3].Ours(o),10:F2} {g.Value("state_hash_ms"),10:F2} {rows[4].Ours(o),10:F2} {g.Value("commit_ms"),10:F2}");
}
return 0;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i + 1 < args.Length; i++)
        if (args[i].StartsWith("--")) result[args[i][2..]] = args[i + 1];
    return result;
}

static double Percentile(List<double> values, double percentile)
{
    if (values.Count == 0) return double.NaN;
    var sorted = values.OrderBy(v => v).ToList();
    var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
    return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
}

static Dictionary<long, Block> ReadGeth(string path)
{
    var result = new Dictionary<long, Block>();
    foreach (var raw in File.ReadLines(path))
    {
        var line = Regex.Replace(raw, "\u001b\\[[0-9;]*m", "");
        var start = line.IndexOf('{');
        var end = line.LastIndexOf('}');
        if (start < 0 || end < start || !line.Contains("\"Slow block\"")) continue;
        using var document = JsonDocument.Parse(line.Substring(start, end - start + 1));
        var root = document.RootElement;
        var timing = root.GetProperty("timing");
        var reads = root.GetProperty("state_reads");
        var writes = root.GetProperty("state_writes");
        var block = new Block();
        block.Fields["total_ms"] = timing.GetProperty("total_ms").GetDouble();
        block.Fields["execution_ms"] = timing.GetProperty("execution_ms").GetDouble();
        block.Fields["state_read_ms"] = timing.GetProperty("state_read_ms").GetDouble();
        block.Fields["state_hash_ms"] = timing.GetProperty("state_hash_ms").GetDouble();
        block.Fields["commit_ms"] = timing.GetProperty("commit_ms").GetDouble();
        block.Fields["mgas_per_sec"] = root.GetProperty("throughput").GetProperty("mgas_per_sec").GetDouble();
        block.Fields["accounts_loaded"] = reads.GetProperty("accounts").GetDouble();
        block.Fields["storage_loaded"] = reads.GetProperty("storage_slots").GetDouble();
        block.Fields["code_loaded"] = reads.GetProperty("code").GetDouble();
        block.Fields["code_load_bytes"] = reads.GetProperty("code_bytes").GetDouble();
        block.Fields["accounts_updated"] = writes.GetProperty("accounts").GetDouble();
        block.Fields["accounts_deleted"] = writes.GetProperty("accounts_deleted").GetDouble();
        block.Fields["storage_updated"] = writes.GetProperty("storage_slots").GetDouble();
        block.Fields["storage_deleted"] = writes.GetProperty("storage_slots_deleted").GetDouble();
        block.Fields["code_updated"] = writes.GetProperty("code").GetDouble();
        block.Fields["code_update_bytes"] = writes.GetProperty("code_bytes").GetDouble();
        result[root.GetProperty("block").GetProperty("number").GetInt64()] = block;
    }
    return result;
}

static Dictionary<long, Block> ReadOurs(string path)
{
    var result = new Dictionary<long, Block>();
    var marker = "block.exec.stats ";
    foreach (var raw in File.ReadLines(path))
    {
        var at = raw.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) continue;
        var block = new Block();
        foreach (var token in raw[(at + marker.Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = token.IndexOf('=');
            if (equals <= 0) continue;
            if (double.TryParse(token[(equals + 1)..].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                block.Fields[token[..equals]] = value;
        }
        if (block.Fields.TryGetValue("block", out var number)) result[(long)number] = block;
    }
    return result;
}

sealed class Block
{
    public Dictionary<string, double> Fields { get; } = new();
    public double Value(string name) => Fields.TryGetValue(name, out var v) ? v : 0;
    public double Ms(string name) => Value(name);
}
