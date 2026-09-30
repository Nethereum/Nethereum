using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.ReceiptProbe
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || args.Any(a => a == "--help" || a == "-h"))
            {
                PrintHelp();
                return 0;
            }

            string peer = GetArg(args, "--peer");
            ulong from = ulong.Parse(GetArg(args, "--from"));
            ulong count = ulong.Parse(GetArg(args, "--count"));
            ulong step = ulong.Parse(GetArgOrDefault(args, "--step", "1"));
            int timeoutSec = int.Parse(GetArgOrDefault(args, "--timeout-sec", "30"));
            string csv = TryGetArg(args, "--csv");
            int batchSize = int.Parse(GetArgOrDefault(args, "--batch-size", "1"));

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            Console.WriteLine($"Dialing peer …");
            SyncPeerSession session;
            try
            {
                session = await SyncPeerSession.ConnectAsync(
                    peer, TimeSpan.FromSeconds(timeoutSec), cts.Token,
                    MainnetGenesisConstants.BlockHashHex.HexToByteArray(), (ulong)MainnetGenesisConstants.ChainId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: {ex.GetType().Name}: {ex.Message}");
                return 2;
            }

            Console.WriteLine(
                $"Connected: eth/{session.EthVersion} host={session.PeerHost} " +
                $"client=\"{session.PeerClientId}\" latest={session.PeerLatestBlock}");
            Console.WriteLine();

            StreamWriter csvWriter = null;
            if (!string.IsNullOrEmpty(csv))
            {
                csvWriter = new StreamWriter(csv);
                csvWriter.WriteLine(
                    "block,block_hash,header_receipt_hash,computed_receipts_root,match," +
                    "receipt_count,wire_bytes_total,post_state_lengths");
            }

            var rootsProvider = PatriciaBlockRootsProvider.Instance;
            var hashProvider = RlpKeccakBlockHashProvider.Instance;

            int total = 0, matches = 0, mismatches = 0, errors = 0, noReceipts = 0;
            var postStateLenHist = new Dictionary<int, long>();

            try
            {
                ulong b = from;
                ulong endExclusive = from + count;
                while (b < endExclusive && !cts.IsCancellationRequested)
                {
                    int blocksThisBatch = (int)Math.Min((ulong)batchSize, endExclusive - b);
                    var headers = await session.GetHeadersAsync(b, (ulong)blocksThisBatch, cts.Token);
                    if (headers.Count == 0)
                    {
                        Console.WriteLine($"block {b}: NO HEADER (peer returned empty)");
                        errors += blocksThisBatch;
                        total += blocksThisBatch;
                        b += step * (ulong)blocksThisBatch;
                        continue;
                    }

                    var hashes = new List<byte[]>(headers.Count);
                    foreach (var h in headers) hashes.Add(hashProvider.ComputeBlockHash(h));

                    List<List<Receipt>> receiptsByBlock;
                    try
                    {
                        receiptsByBlock = await session.GetReceiptsAsync(hashes, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"block {b}+{headers.Count}: GET-RECEIPTS FAILED {ex.GetType().Name}: {ex.Message}");
                        errors += headers.Count;
                        total += headers.Count;
                        b += step * (ulong)headers.Count;
                        continue;
                    }

                    for (int i = 0; i < headers.Count; i++)
                    {
                        total++;
                        var header = headers[i];
                        var hash = hashes[i];
                        ulong blockNum = b + (ulong)i * step;
                        var expectedHex = header.ReceiptHash.ToHex();

                        if (i >= receiptsByBlock.Count)
                        {
                            Console.WriteLine($"block {blockNum}: NO RECEIPTS (peer truncated batch at {receiptsByBlock.Count}/{headers.Count})");
                            noReceipts++;
                            continue;
                        }

                        var receipts = receiptsByBlock[i] ?? new List<Receipt>();
                        var computed = rootsProvider.CalculateReceiptsRoot(receipts);
                        var computedHex = computed.ToHex();
                        bool match = string.Equals(expectedHex, computedHex, StringComparison.OrdinalIgnoreCase);

                        long wireBytesTotal = 0;
                        var lens = new List<int>(receipts.Count);
                        foreach (var r in receipts)
                        {
                            int len = r.PostStateOrStatus?.Length ?? 0;
                            lens.Add(len);
                            postStateLenHist[len] = postStateLenHist.GetValueOrDefault(len) + 1;
                            wireBytesTotal += ReceiptEncoder.Current.Encode(r).Length;
                        }
                        var lensSummary = SummarizeLens(lens);

                        string symbol = match ? "OK " : "MISS";
                        Console.WriteLine(
                            $"block {blockNum,10} {symbol}  expected={expectedHex.Substring(0, 16)}…  " +
                            $"computed={computedHex.Substring(0, 16)}…  rcpts={receipts.Count,4}  " +
                            $"wire={wireBytesTotal,7}B  postStateLens={lensSummary}");

                        if (match) matches++; else mismatches++;

                        csvWriter?.WriteLine(
                            $"{blockNum},0x{hash.ToHex()},0x{expectedHex},0x{computedHex},{match}," +
                            $"{receipts.Count},{wireBytesTotal},\"{string.Join(';', lens)}\"");
                    }

                    b += step * (ulong)headers.Count;
                }
            }
            finally
            {
                try { csvWriter?.Flush(); csvWriter?.Dispose(); } catch { }
                try { session.Dispose(); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine($"=== Summary ===");
            Console.WriteLine($"Total blocks probed:  {total}");
            Console.WriteLine($"  Merkle match:       {matches}");
            Console.WriteLine($"  Merkle mismatch:    {mismatches}");
            Console.WriteLine($"  No receipts:        {noReceipts}");
            Console.WriteLine($"  Errors:             {errors}");
            Console.WriteLine();
            Console.WriteLine($"PostStateOrStatus byte-length histogram (across all receipts):");
            foreach (var kv in postStateLenHist.OrderBy(k => k.Key))
            {
                string label = kv.Key switch
                {
                    0 => "0 bytes (Byzantium status=0/failure rlp-encoded as empty)",
                    1 => "1 byte  (Byzantium+ status: 0x01 success / 0x80 failure)",
                    32 => "32 bytes (pre-Byzantium PostState root — canonical)",
                    _ => $"{kv.Key} bytes (unusual)"
                };
                Console.WriteLine($"  {label,-65} {kv.Value,10} receipts");
            }

            return mismatches > 0 ? 1 : 0;
        }

        static void PrintHelp()
        {
            Console.WriteLine("nethereum-receiptprobe — probe a DevP2P peer's receipts and validate merkle root");
            Console.WriteLine();
            Console.WriteLine("Usage: nethereum-receiptprobe --peer <enode> --from N --count N [opts]");
            Console.WriteLine();
            Console.WriteLine("Required:");
            Console.WriteLine("  --peer <enode://pubkey@host:port>");
            Console.WriteLine("  --from <block-number>");
            Console.WriteLine("  --count <number-of-blocks>");
            Console.WriteLine();
            Console.WriteLine("Optional:");
            Console.WriteLine("  --step N            (default 1)  Sample every Nth block");
            Console.WriteLine("  --batch-size N      (default 1)  Headers/receipts per round-trip");
            Console.WriteLine("  --timeout-sec N     (default 30) Per-request timeout");
            Console.WriteLine("  --csv <path>                     Detailed per-block CSV output");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0=all match, 1=at least one merkle mismatch, 2=connection failed");
        }

        static string SummarizeLens(List<int> lens)
        {
            if (lens.Count == 0) return "[]";
            int min = lens.Min();
            int max = lens.Max();
            if (min == max) return $"{min}×{lens.Count}";
            var groups = lens.GroupBy(x => x).OrderBy(g => g.Key);
            return string.Join(",", groups.Select(g => $"{g.Key}×{g.Count()}"));
        }

        static string GetArg(string[] args, string name)
        {
            string v = TryGetArg(args, name);
            if (v == null) throw new ArgumentException($"missing required argument {name}");
            return v;
        }

        static string GetArgOrDefault(string[] args, string name, string defaultValue)
            => TryGetArg(args, name) ?? defaultValue;

        static string TryGetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
