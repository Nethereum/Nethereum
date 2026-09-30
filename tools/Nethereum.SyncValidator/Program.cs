using System.Diagnostics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.SyncValidator;
using Nethereum.Web3;

var opts = CliOptions.Parse(args);
if (opts is null) return 2;

var reference = new Web3(opts.ReferenceUrl);
var ours = new Web3(opts.OurUrl);

var ct = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; ct.Cancel(); };

long referenceLatest = (long)(await reference.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
long oursLatest = (long)(await ours.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
long to = opts.To ?? Math.Min(referenceLatest, oursLatest);

Console.WriteLine($"reference latest = {referenceLatest:N0}");
Console.WriteLine($"our    latest = {oursLatest:N0}");

if (opts.Mode is Mode.State)
{
    var stateBlock = opts.Block ?? Math.Min(referenceLatest, oursLatest);
    return await StateAcceptance.RunAsync(reference, ours, stateBlock, opts.Accounts, opts.Samples, ct.Token);
}
Console.WriteLine($"validating [{opts.From:N0} .. {to:N0}] sample-every={opts.SampleEvery} mode={opts.Mode} max-mismatches={opts.MaxMismatches}");
Console.WriteLine(new string('=', 80));

var totalMismatches = 0;
var compared = 0;
var sw = Stopwatch.StartNew();
var lastLog = sw.Elapsed;

var rpcErrors = 0;
for (long n = opts.From; n <= to && !ct.IsCancellationRequested; n += opts.SampleEvery)
{
    var bp = new BlockParameter(new HexBigInteger(n));
    List<string> blockDiffs = new(), receiptDiffs = new();

    try
    {
        if (opts.Mode is Mode.Blocks or Mode.Both)
        {
            var er = await reference.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(bp);
            var ou = await ours.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(bp);
            blockDiffs = DtoComparer.Compare(er, ou, $"block[{n}]");
        }

        if (opts.Mode is Mode.Receipts or Mode.Both)
        {
            var er = await reference.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(bp);
            var ou = await ours.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(bp);
            receiptDiffs = DtoComparer.Compare(er, ou, $"receipts[{n}]");
        }
    }
    catch (Exception ex)
    {
        // Transient RPC errors (timeouts, slow peers, momentary stalls) shouldn't
        // abort a multi-hundred-block sweep. Log and continue — a flaky block is
        // not the same as a divergent block.
        rpcErrors++;
        Console.WriteLine($"RPC-ERROR at block {n:N0}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        continue;
    }

    compared++;
    var allDiffs = blockDiffs.Concat(receiptDiffs).ToList();
    if (allDiffs.Count > 0)
    {
        totalMismatches++;
        Console.WriteLine();
        Console.WriteLine($"MISMATCH at block {n:N0} ({allDiffs.Count} differing fields):");
        foreach (var d in allDiffs.Take(30)) Console.WriteLine("  " + d);
        if (allDiffs.Count > 30) Console.WriteLine($"  ... and {allDiffs.Count - 30} more");
        Console.WriteLine();
        if (totalMismatches >= opts.MaxMismatches)
        {
            Console.WriteLine($"reached --max-mismatches={opts.MaxMismatches}, stopping");
            break;
        }
    }

    if (sw.Elapsed - lastLog > TimeSpan.FromSeconds(5))
    {
        Console.WriteLine($"progress: block={n:N0} compared={compared:N0} mismatches={totalMismatches} rate={compared / sw.Elapsed.TotalSeconds:F1} blocks/s");
        lastLog = sw.Elapsed;
    }
}

Console.WriteLine(new string('=', 80));
Console.WriteLine($"DONE: compared={compared:N0} mismatches={totalMismatches} rpc-errors={rpcErrors} elapsed={sw.Elapsed:hh\\:mm\\:ss}");
return totalMismatches == 0 ? 0 : 1;

internal enum Mode { Blocks, Receipts, Both, State }

internal sealed class CliOptions
{
    public required string ReferenceUrl { get; init; }
    public required string OurUrl { get; init; }
    public required long From { get; init; }
    public long? To { get; init; }
    public long SampleEvery { get; init; } = 1;
    public Mode Mode { get; init; } = Mode.Both;
    public int MaxMismatches { get; init; } = 1;
    public long? Block { get; init; }
    public string[]? Accounts { get; init; }
    public int Samples { get; init; } = 16;

    public static CliOptions? Parse(string[] args)
    {
        string? reference = null, ours = null, mode = "both", accounts = null;
        long? from = null, to = null, sample = 1, block = null;
        int max = 1, samples = 16;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--reference": case "--erigon": reference = args[++i]; break;
                case "--our": ours = args[++i]; break;
                case "--from": from = long.Parse(args[++i]); break;
                case "--to": to = long.Parse(args[++i]); break;
                case "--sample-every": sample = long.Parse(args[++i]); break;
                case "--mode": mode = args[++i]; break;
                case "--max-mismatches": max = int.Parse(args[++i]); break;
                case "--block": block = long.Parse(args[++i]); break;
                case "--accounts": accounts = args[++i]; break;
                case "--samples": samples = int.Parse(args[++i]); break;
                case "-h":
                case "--help": PrintHelp(); return null;
                default: Console.Error.WriteLine($"unknown arg: {args[i]}"); PrintHelp(); return null;
            }
        }
        var parsedMode = mode!.ToLowerInvariant() switch
        {
            "blocks" => Mode.Blocks,
            "receipts" => Mode.Receipts,
            "both" => Mode.Both,
            "state" => Mode.State,
            _ => throw new ArgumentException($"--mode must be blocks|receipts|both|state, got {mode}")
        };
        if (reference is null || ours is null || (from is null && parsedMode != Mode.State)) { PrintHelp(); return null; }
        return new CliOptions
        {
            ReferenceUrl = reference,
            OurUrl = ours,
            From = from ?? 0,
            To = to,
            SampleEvery = sample!.Value,
            Mode = parsedMode,
            MaxMismatches = max,
            Block = block,
            Accounts = accounts?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Samples = samples
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"nethereum-syncvalidator: deep-compare blocks + receipts between two RPC endpoints

Usage:
  nethereum-syncvalidator --reference <url> --our <url> --from <N> [options]

Required:
  --reference URL     canonical RPC endpoint (alias: --erigon) (e.g. http://192.168.1.40:8545)
  --our URL           our node's RPC endpoint (e.g. http://192.168.1.40:8548)
  --from N            first block to compare

Optional:
  --to N              last block (default: MIN of both endpoints' latest)
  --sample-every N    stride (default 1; e.g. 1000 = every 1000th block)
  --mode M            blocks | receipts | both | state (default both)
  --max-mismatches N  stop after N mismatched blocks (default 1 = fail fast)

State acceptance (--mode state; --from not required):
  --block N           block to prove state at (default: MIN of both latests; use the snap pivot)
  --accounts a,b,...  fixture addresses (default: canonical EOA + contract fixtures)
  --samples N         additional seeded-random addresses, exclusion-proof compared (default 16)
  Byte-compares eth_getProof + balance + nonce + code per account against the reference.
  Exit 0 = state provably matches the reference at that block.");
    }
}
