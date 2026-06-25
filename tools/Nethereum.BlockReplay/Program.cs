using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.ModelFactories;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// Block-replay CLI: fetches blocks from any JSON-RPC endpoint, replays
    /// each through the host <see cref="BlockExecutor"/> engine with reads
    /// served by an <see cref="RpcNodeDataService"/> over the same RPC, and
    /// validates execution by comparing per-tx receipts + per-touched-account
    /// post-state against canonical RPC. Sub-commands:
    ///   replay-block — single block
    ///   replay-range — block range with optional sampling
    /// </summary>
    internal static class Program
    {
        private const int MainnetChainId = 1;
        private const long MainnetMergeBlock = 15_537_394L;

        public static async Task<int> Main(string[] argv)
        {
            if (argv.Length == 0 || argv[0] == "--help" || argv[0] == "-h")
            {
                PrintHelp();
                return 0;
            }
            try
            {
                return argv[0] switch
                {
                    "replay-block" => await ReplayBlockCommandAsync(argv.Skip(1).ToArray()),
                    "replay-range" => await ReplayRangeCommandAsync(argv.Skip(1).ToArray()),
                    _ => Fail($"unknown command: {argv[0]}")
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static int Fail(string msg)
        {
            Console.Error.WriteLine($"error: {msg}");
            PrintHelp();
            return 1;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Nethereum BlockReplay — replay blocks from any JSON-RPC endpoint");
            Console.WriteLine();
            Console.WriteLine("commands:");
            Console.WriteLine("  replay-block --block N --rpc <url> [--out-witness <path>] [--emit-fixture <path>] [--trace-tx <i> --out-trace <path>] [--fork <name>]");
            Console.WriteLine("  replay-range --from N --to M --rpc <url> [--every K] [--stop-on-mismatch]");
            Console.WriteLine();
            Console.WriteLine("options:");
            Console.WriteLine("  --block <N>           block number to replay (replay-block)");
            Console.WriteLine("  --from <N> --to <M>   inclusive range (replay-range)");
            Console.WriteLine("  --every <K>           replay every Kth block in the range (default: 1)");
            Console.WriteLine("  --stop-on-mismatch    abort the range on first divergence");
            Console.WriteLine("  --rpc <url>           JSON-RPC endpoint (Geth, Erigon, Nethermind, …)");
            Console.WriteLine("  --out-witness <path>  write the captured BinaryBlockWitness to disk");
            Console.WriteLine("  --emit-fixture <path> write a MainnetBlockFixture JSON file (block-replay diff fixture)");
            Console.WriteLine("  --trace-tx <i>        enable opcode tracing for tx index i (production path; pairs with --out-trace)");
            Console.WriteLine("  --out-trace <path>    JSONL opcode-trace dump of the --trace-tx tx");
            Console.WriteLine("  --fork <name>         override fork resolution (frontier..osaka)");
        }

        // === replay-block command ===

        private static async Task<int> ReplayBlockCommandAsync(string[] argv)
        {
            long? blockNumber = null;
            string rpcUrl = null;
            string outWitness = null;
            string outFixture = null;
            string outTrace = null;
            int? traceTxIndex = null;
            string forkOverride = null;
            string cacheDir = null;

            for (int i = 0; i < argv.Length; i++)
            {
                switch (argv[i])
                {
                    case "--block": blockNumber = long.Parse(argv[++i]); break;
                    case "--rpc": rpcUrl = argv[++i]; break;
                    case "--out-witness": outWitness = argv[++i]; break;
                    case "--emit-fixture": outFixture = argv[++i]; break;
                    case "--trace-tx": traceTxIndex = int.Parse(argv[++i]); break;
                    case "--out-trace": outTrace = argv[++i]; break;
                    case "--fork": forkOverride = argv[++i]; break;
                    case "--cache-dir": cacheDir = argv[++i]; break;
                    default: return Fail($"unknown arg: {argv[i]}");
                }
            }
            if (blockNumber == null) return Fail("--block is required");
            if (string.IsNullOrEmpty(rpcUrl)) return Fail("--rpc is required");
            if (traceTxIndex.HasValue && string.IsNullOrEmpty(outTrace))
                return Fail("--trace-tx requires --out-trace");

            var web3 = new Web3.Web3(rpcUrl);
            var chainIdHex = await web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false);
            Console.WriteLine($"replay-block N={blockNumber:N0} rpc={rpcUrl}");
            Console.WriteLine($"  chainId: {chainIdHex.Value}");

            using var cache = cacheDir == null ? null : new RpcReplayCache(cacheDir);
            var r = await ReplayOneAsync(web3, blockNumber.Value, chainIdHex.Value, forkOverride, outWitness, outFixture, traceTxIndex, outTrace, verbose: true, cache: cache).ConfigureAwait(false);
            if (cache != null)
                Console.WriteLine($"  cache:                hits={cache.Hits:N0} misses={cache.Misses:N0} hit-rate={(cache.Hits + cache.Misses == 0 ? 0 : 100.0 * cache.Hits / (cache.Hits + cache.Misses)):F1}%");
            return r.AllMatch ? 0 : (r.ExecutionFailed ? 2 : 3);
        }

        // === replay-range command ===

        private static async Task<int> ReplayRangeCommandAsync(string[] argv)
        {
            long? from = null, to = null;
            int every = 1;
            int parallelism = 1;
            string rpcUrl = null;
            bool stopOnMismatch = false;
            string forkOverride = null;
            string cacheDir = null;

            for (int i = 0; i < argv.Length; i++)
            {
                switch (argv[i])
                {
                    case "--from": from = long.Parse(argv[++i]); break;
                    case "--to": to = long.Parse(argv[++i]); break;
                    case "--every": every = int.Parse(argv[++i]); break;
                    case "--parallelism": parallelism = int.Parse(argv[++i]); break;
                    case "--rpc": rpcUrl = argv[++i]; break;
                    case "--stop-on-mismatch": stopOnMismatch = true; break;
                    case "--fork": forkOverride = argv[++i]; break;
                    case "--cache-dir": cacheDir = argv[++i]; break;
                    default: return Fail($"unknown arg: {argv[i]}");
                }
            }
            if (from == null || to == null) return Fail("--from and --to are required");
            if (string.IsNullOrEmpty(rpcUrl)) return Fail("--rpc is required");
            if (every < 1) return Fail("--every must be >= 1");
            if (parallelism < 1) return Fail("--parallelism must be >= 1");

            var web3 = new Web3.Web3(rpcUrl);
            var chainIdHex = await web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false);
            using var sharedCache = cacheDir == null ? null : new RpcReplayCache(cacheDir);
            Console.WriteLine($"replay-range from={from:N0} to={to:N0} every={every} parallelism={parallelism} chainId={chainIdHex.Value} rpc={rpcUrl}{(cacheDir == null ? "" : " cache=" + cacheDir)}");
            Console.WriteLine();
            Console.WriteLine($"  {"block",12}  {"fork",-15}  {"txs",5}  {"gas",10}  {"status",10}  {"accts",10}  {"slots",10}  {"sec",6}  result");
            Console.WriteLine("  " + new string('-', 105));

            var blocks = new List<long>();
            for (long n = from.Value; n <= to.Value; n += every) blocks.Add(n);
            int total = blocks.Count;
            int passed = 0, failed = 0;
            var failures = new System.Collections.Concurrent.ConcurrentBag<long>();
            var outputLock = new object();
            var cts = new System.Threading.CancellationTokenSource();
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            await Parallel.ForEachAsync(blocks,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cts.Token },
                async (n, ct) =>
                {
                    try
                    {
                        var r = await ReplayOneAsync(web3, n, chainIdHex.Value, forkOverride, outWitness: null, outFixture: null, traceTxIndex: null, outTrace: null, verbose: false, cache: sharedCache).ConfigureAwait(false);
                        string verdict = r.AllMatch ? "✅" : (r.ExecutionFailed ? "💥 " + r.ErrorMessage : "❌");
                        lock (outputLock)
                        {
                            Console.WriteLine(
                                $"  {n,12:N0}  {r.Fork,-15}  {r.TxCount,5}  {r.GasMatch}/{r.TxCount,-7}  " +
                                $"{r.StatusMatch}/{r.TxCount,-7}  {r.AccMatch}/{r.AccTotal,-7}  {r.SlotMatch}/{r.SlotTotal,-7}  " +
                                $"{r.ElapsedSec,6:F1}  {verdict}");
                        }
                        if (r.AllMatch) System.Threading.Interlocked.Increment(ref passed);
                        else
                        {
                            System.Threading.Interlocked.Increment(ref failed);
                            failures.Add(n);
                            if (stopOnMismatch) cts.Cancel();
                        }
                    }
                    catch (OperationCanceledException) { /* aborted by stop-on-mismatch */ }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.Increment(ref failed);
                        failures.Add(n);
                        lock (outputLock) Console.WriteLine($"  {n,12:N0}  💥 exception: {ex.Message}");
                        if (stopOnMismatch) cts.Cancel();
                    }
                }).ConfigureAwait(false);

            totalSw.Stop();
            Console.WriteLine();
            var sortedFailures = failures.OrderBy(x => x).ToList();
            Console.WriteLine($"  total={total} passed={passed} failed={failed} wall={totalSw.Elapsed.TotalSeconds:F1}s avg={(total == 0 ? 0 : totalSw.Elapsed.TotalSeconds / total):F2}s/block");
            if (sharedCache != null)
                Console.WriteLine($"  cache: hits={sharedCache.Hits:N0} misses={sharedCache.Misses:N0} hit-rate={(sharedCache.Hits + sharedCache.Misses == 0 ? 0 : 100.0 * sharedCache.Hits / (sharedCache.Hits + sharedCache.Misses)):F1}%");
            if (sortedFailures.Count > 0 && sortedFailures.Count <= 50)
                Console.WriteLine($"  failed blocks: {string.Join(", ", sortedFailures)}");
            Console.WriteLine($"  OVERALL: {(failed == 0 ? "✅ all canonical" : "❌ " + failed + " divergence(s)")}");
            return failed == 0 ? 0 : 3;
        }

        // === Core replay + validation, reusable by both commands ===

        private struct ReplayResult
        {
            public long BlockNumber;
            public string Fork;
            public int TxCount;
            public int GasMatch, GasDiverge;
            public int StatusMatch, StatusDiverge;
            public int AccMatch, AccDiverge, AccTotal;
            public int SlotMatch, SlotDiverge, SlotTotal;
            public double ElapsedSec;
            public bool ExecutionFailed;
            public string ErrorMessage;
            public bool AllMatch => !ExecutionFailed && GasDiverge == 0 && StatusDiverge == 0 && AccDiverge == 0 && SlotDiverge == 0;
        }

        private static async Task<ReplayResult> ReplayOneAsync(
            Web3.Web3 web3, long blockNumber, BigInteger chainId,
            string forkOverride, string outWitness, string outFixture,
            int? traceTxIndex, string outTrace, bool verbose,
            RpcReplayCache cache = null)
        {
            var eth = web3.Eth;
            var r = new ReplayResult { BlockNumber = blockNumber };

            // 1. Fetch block with transactions
            var rpcBlock = await eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber))).ConfigureAwait(false);
            if (rpcBlock == null)
            {
                r.ExecutionFailed = true; r.ErrorMessage = "block not returned by RPC"; return r;
            }
            r.TxCount = rpcBlock.Transactions.Length;

            // 2. Build header. Guard pre-London (BaseFeePerGas null).
            if (rpcBlock.BaseFeePerGas == null)
                rpcBlock.BaseFeePerGas = new HexBigInteger(0);
            var header = BlockHeaderRPCFactory.FromRPC(rpcBlock);
            ApplyExtraHeaderFields(header, rpcBlock);

            if (verbose)
            {
                Console.WriteLine($"  canonical state root: 0x{header.StateRoot.ToHex()}");
                Console.WriteLine($"  txs:                  {r.TxCount}");
                Console.WriteLine($"  miner:                {header.Coinbase}");
                Console.WriteLine($"  timestamp:            {header.Timestamp}");
            }

            // 3. Build txs via eth_getRawTransactionByBlockNumberAndIndex (rpcTx.Input is call data, not signed RLP).
            var txEntries = new List<TxEntry>(r.TxCount);
            var rawTxHexes = new List<string>(r.TxCount);
            var blockNumHex = "0x" + blockNumber.ToString("x");
            for (int i = 0; i < r.TxCount; i++)
            {
                var indexHex = "0x" + i.ToString("x");
                var rawTxHex = await web3.Client.SendRequestAsync<string>(
                    new Nethereum.JsonRpc.Client.RpcRequest(0, "eth_getRawTransactionByBlockNumberAndIndex",
                        blockNumHex, indexHex)).ConfigureAwait(false);
                if (string.IsNullOrEmpty(rawTxHex))
                {
                    r.ExecutionFailed = true; r.ErrorMessage = $"raw tx[{i}] missing"; return r;
                }
                var signedTx = TransactionFactory.CreateTransaction(rawTxHex.HexToByteArray());
                txEntries.Add(new TxEntry(signedTx, rpcBlock.Transactions[i].From));
                rawTxHexes.Add(rawTxHex);
            }

            // 4. Wire stores: RpcNodeDataService → ReadOnlyStateReaderStore → ReadOnlyStateStoreWrapper → TouchRecordingStore.
            var parentBlockParam = new BlockParameter(new HexBigInteger(blockNumber - 1));
            IStateReader rpcReader = new RpcNodeDataService(eth, parentBlockParam);
            if (cache != null)
                rpcReader = new CachingStateReader(rpcReader, cache, blockNumber - 1);
            var touchRecorder = new TouchRecordingStore(new ReadOnlyStateStoreWrapper(new ReadOnlyStateReaderStore(rpcReader)));
            IStateStore stateStore = touchRecorder;
            var blockStore = new RpcBlockStore(eth);
            var trieStore = new InMemoryTrieNodeStore();
            var calculator = new IncrementalStateRootCalculator(stateStore, trieStore);

            // 5. Activations + reward policy.
            IChainActivations activations;
            IRewardPolicy rewardPolicy;
            if (chainId == MainnetChainId && forkOverride == null)
            {
                activations = MainnetChainActivations.Instance;
                rewardPolicy = blockNumber < MainnetMergeBlock
                    ? (IRewardPolicy)EthereumProofOfWorkRewardPolicy.Instance
                    : NoRewardPolicy.Instance;
            }
            else
            {
                var f = forkOverride != null ? HardforkNames.Parse(forkOverride) : HardforkName.Prague;
                activations = new FixedChainActivations(f);
                rewardPolicy = NoRewardPolicy.Instance;
            }

            var activeFork = activations.ResolveAt(blockNumber, (ulong)header.Timestamp);
            r.Fork = activeFork.ToString().ToLowerInvariant();
            var chainConfig = new ChainConfig
            {
                ChainId = chainId,
                BaseFee = header.BaseFee ?? BigInteger.Zero,
                Coinbase = header.Coinbase ?? AddressUtil.ZERO_ADDRESS,
                Hardfork = r.Fork
            };
            var engine = new BlockExecutor(
                stateStore, blockStore, activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: f => Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calculator,
                rewardPolicy: rewardPolicy,
                trieNodeStore: trieStore);

            IList<WithdrawalEntry> withdrawals = null;
            if (rpcBlock.Withdrawals != null && rpcBlock.Withdrawals.Length > 0)
                withdrawals = rpcBlock.Withdrawals.Select(w => new WithdrawalEntry(w.Address, w.Amount.Value)).ToList();

            bool captureWitness = outWitness != null;
            var opts = new BlockExecutionOptions
            {
                ReadOnly = false,
                CaptureWitness = captureWitness,
                ParentBeaconBlockRoot = header.ParentBeaconBlockRoot,
                TraceTxIndex = traceTxIndex
            };

            if (verbose)
            {
                Console.WriteLine($"  fork:                 {r.Fork}");
                Console.WriteLine();
                Console.WriteLine("executing...");
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await engine.ExecuteAsync(header, txEntries, uncles: null, withdrawals, opts);
            sw.Stop();
            r.ElapsedSec = sw.Elapsed.TotalSeconds;
            if (verbose) Console.WriteLine($"  elapsed:              {r.ElapsedSec:F2}s");

            if (result.Exception != null)
            {
                r.ExecutionFailed = true;
                r.ErrorMessage = result.ErrorMessage ?? result.Exception.GetType().Name;
                if (verbose) Console.Error.WriteLine($"  EXECUTION FAILED:     {r.ErrorMessage}");
                return r;
            }

            // 6. Validation block — RPC-bound. Batch every per-tx / per-account
            //    / per-slot lookup in parallel via Task.WhenAll. The bottleneck
            //    is RTT, not CPU.
            var atBlockN = new BlockParameter(new HexBigInteger(blockNumber));

            // Per-tx receipt diff: one eth_getBlockReceipts instead of N per-tx
            // calls. Plain trie reads on the server, no re-execution, single
            // round-trip per block.
            var theirReceipts = await eth.Blocks.GetBlockReceiptsByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber)).ConfigureAwait(false);
            for (int i = 0; i < r.TxCount; i++)
            {
                var ours = result.Receipts[i];
                var theirs = theirReceipts[i];
                long ourGas = (long)ours.GasUsed;
                long theirGas = (long)theirs.GasUsed.Value;
                if (ourGas == theirGas) r.GasMatch++; else
                {
                    r.GasDiverge++;
                    if (verbose) Console.WriteLine($"  GAS DIVERGE tx[{i}] {rpcBlock.Transactions[i].TransactionHash}: ours={ourGas} canonical={theirGas}");
                }
                bool ourSuccess = ours.Receipt != null && ours.Receipt.IsStatusReceipt
                    && ours.Receipt.PostStateOrStatus.Length == 1 && ours.Receipt.PostStateOrStatus[0] == 1;
                bool theirSuccess = theirs.Status != null && theirs.Status.Value == BigInteger.One;
                if (ourSuccess == theirSuccess) r.StatusMatch++; else
                {
                    r.StatusDiverge++;
                    if (verbose) Console.WriteLine($"  STATUS DIVERGE tx[{i}]: ours={ourSuccess} canonical={theirSuccess}");
                }
            }

            // Per-touched-account + per-touched-slot validation via
            // JSON-RPC batching: chunks of independent flat-state reads
            // shipped in a single HTTP frame each. Erigon handles batched
            // getBalance/getTransactionCount/getStorageAt cheaply (no PMT,
            // no re-execution). Concurrent chunks let the server overlap
            // batched reads without serializing a single megabyte request.
            var balanceHandler = (EthGetBalance)eth.GetBalance;
            var nonceHandler = (EthGetTransactionCount)eth.Transactions.GetTransactionCount;
            var storageHandler = (EthGetStorageAt)eth.GetStorageAt;
            var rpcClient = web3.Client;
            // Erigon defaults to 100 calls per JSON-RPC batch (--rpc.batch.limit).
            // Accounts emit 2 calls per addr, slots 1 call per pair.
            const int acctChunkSize = 50;   // → 100 calls/batch
            const int slotChunkSize = 100;  // → 100 calls/batch

            var dirtyAddrs = new List<string>(touchRecorder.TouchedAccounts.Keys);
            r.AccTotal = dirtyAddrs.Count;
            var ourAccountTasks = dirtyAddrs.Select(a => stateStore.GetAccountAsync(a)).ToArray();
            var theirBalances = new HexBigInteger[dirtyAddrs.Count];
            var theirNonces = new HexBigInteger[dirtyAddrs.Count];
            var accountChunkTasks = new List<Task>();
            for (int s = 0; s < dirtyAddrs.Count; s += acctChunkSize)
            {
                int start = s;
                int end = Math.Min(s + acctChunkSize, dirtyAddrs.Count);
                accountChunkTasks.Add(Task.Run(async () =>
                {
                    var batch = new RpcRequestResponseBatch { AcceptPartiallySuccessful = true };
                    var balItems = new RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger>[end - start];
                    var nonceItems = new RpcRequestResponseBatchItem<EthGetTransactionCount, HexBigInteger>[end - start];
                    int id = 0;
                    for (int i = start; i < end; i++)
                    {
                        var b = new RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger>(
                            balanceHandler, balanceHandler.BuildRequest(dirtyAddrs[i], atBlockN, id++));
                        var n = new RpcRequestResponseBatchItem<EthGetTransactionCount, HexBigInteger>(
                            nonceHandler, nonceHandler.BuildRequest(dirtyAddrs[i], atBlockN, id++));
                        batch.BatchItems.Add(b);
                        batch.BatchItems.Add(n);
                        balItems[i - start] = b;
                        nonceItems[i - start] = n;
                    }
                    await rpcClient.SendBatchRequestAsync(batch).ConfigureAwait(false);
                    for (int i = 0; i < balItems.Length; i++)
                    {
                        if (balItems[i].HasError)
                            throw new Exception($"acct batch err addr={dirtyAddrs[start + i]} method=getBalance err={balItems[i].RpcError?.Message}");
                        if (nonceItems[i].HasError)
                            throw new Exception($"acct batch err addr={dirtyAddrs[start + i]} method=getTransactionCount err={nonceItems[i].RpcError?.Message}");
                        theirBalances[start + i] = balItems[i].Response;
                        theirNonces[start + i] = nonceItems[i].Response;
                    }
                }));
            }
            await Task.WhenAll(Task.WhenAll(ourAccountTasks), Task.WhenAll(accountChunkTasks)).ConfigureAwait(false);
            for (int i = 0; i < dirtyAddrs.Count; i++)
            {
                var addr = dirtyAddrs[i];
                var ourAccount = ourAccountTasks[i].Result;
                var theirBalance = theirBalances[i];
                var theirNonce = theirNonces[i];
                var ourBalance = ourAccount?.Balance.ToBigInteger() ?? BigInteger.Zero;
                var ourNonce = ourAccount?.Nonce.ToBigInteger() ?? BigInteger.Zero;
                if (ourBalance == theirBalance.Value && ourNonce == theirNonce.Value) r.AccMatch++; else
                {
                    r.AccDiverge++;
                    if (verbose) Console.WriteLine($"  ACCT DIVERGE {addr}: ours bal={ourBalance} nonce={ourNonce} | canonical bal={theirBalance.Value} nonce={theirNonce.Value}");
                }
            }

            var slotPairs = new List<(string addr, BigInteger slot)>();
            foreach (var addr in dirtyAddrs)
            {
                if (!touchRecorder.TouchedSlots.TryGetValue(addr, out var slotSet)) continue;
                foreach (var slot in slotSet.Keys) slotPairs.Add((addr, slot));
            }
            r.SlotTotal = slotPairs.Count;
            var ourSlotTasks = slotPairs.Select(p => stateStore.GetStorageAsync(p.addr, p.slot)).ToArray();
            var theirSlotValues = new string[slotPairs.Count];
            var slotChunkTasks = new List<Task>();
            for (int s = 0; s < slotPairs.Count; s += slotChunkSize)
            {
                int start = s;
                int end = Math.Min(s + slotChunkSize, slotPairs.Count);
                slotChunkTasks.Add(Task.Run(async () =>
                {
                    var batch = new RpcRequestResponseBatch { AcceptPartiallySuccessful = true };
                    var items = new RpcRequestResponseBatchItem<EthGetStorageAt, string>[end - start];
                    int id = 0;
                    for (int i = start; i < end; i++)
                    {
                        var (addr, slot) = slotPairs[i];
                        var item = new RpcRequestResponseBatchItem<EthGetStorageAt, string>(
                            storageHandler,
                            storageHandler.BuildRequest(addr, new HexBigInteger(slot), atBlockN, id++));
                        batch.BatchItems.Add(item);
                        items[i - start] = item;
                    }
                    await rpcClient.SendBatchRequestAsync(batch).ConfigureAwait(false);
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i].HasError)
                            throw new Exception($"slot batch err addr={slotPairs[start + i].addr}@{slotPairs[start + i].slot} err={items[i].RpcError?.Message}");
                        theirSlotValues[start + i] = items[i].Response;
                    }
                }));
            }
            await Task.WhenAll(Task.WhenAll(ourSlotTasks), Task.WhenAll(slotChunkTasks)).ConfigureAwait(false);
            for (int i = 0; i < slotPairs.Count; i++)
            {
                var ourValue = ourSlotTasks[i].Result ?? Array.Empty<byte>();
                var theirValue = StripLeadingZeros(theirSlotValues[i].HexToByteArray());
                var ourTrimmed = StripLeadingZeros(ourValue);
                if (ByteUtil.AreEqual(ourTrimmed, theirValue)) r.SlotMatch++; else
                {
                    r.SlotDiverge++;
                    if (verbose && r.SlotDiverge <= 10)
                        Console.WriteLine($"  SLOT DIVERGE {slotPairs[i].addr}@{slotPairs[i].slot}: ours=0x{ourTrimmed.ToHex()} canonical=0x{theirValue.ToHex()}");
                }
            }

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"  per-tx gas:           {r.GasMatch}/{r.TxCount} match ({(r.GasDiverge == 0 ? "✅" : "❌ " + r.GasDiverge + " diverge")})");
                Console.WriteLine($"  per-tx status:        {r.StatusMatch}/{r.TxCount} match ({(r.StatusDiverge == 0 ? "✅" : "❌ " + r.StatusDiverge + " diverge")})");
                Console.WriteLine($"  touched accounts:     {r.AccMatch}/{r.AccTotal} match ({(r.AccDiverge == 0 ? "✅" : "❌ " + r.AccDiverge + " diverge")})");
                Console.WriteLine($"  touched slots:        {r.SlotMatch}/{r.SlotTotal} match ({(r.SlotDiverge == 0 ? "✅" : "❌ " + r.SlotDiverge + " diverge")})");
                Console.WriteLine();
                Console.WriteLine($"  OVERALL:              {(r.AllMatch ? "✅ all canonical" : "❌ divergence detected")}");
            }

            if (captureWitness && result.WitnessBytes != null)
            {
                await File.WriteAllBytesAsync(outWitness, result.WitnessBytes);
                if (verbose) Console.WriteLine($"  witness:              wrote {result.WitnessBytes.Length} bytes to {outWitness}");
            }

            if (outFixture != null)
            {
                await EmitFixtureAsync(eth, rpcBlock, header, rawTxHexes,
                    dirtyAddrs, slotPairs, blockNumber, outFixture).ConfigureAwait(false);
                if (verbose) Console.WriteLine($"  fixture:              wrote {outFixture}");
            }

            if (traceTxIndex.HasValue && outTrace != null)
            {
                var idx = traceTxIndex.Value;
                if (idx < 0 || idx >= result.Receipts.Count)
                {
                    Console.Error.WriteLine($"  trace: tx index {idx} out of range [0..{result.Receipts.Count - 1}]");
                }
                else
                {
                    var traces = result.Receipts[idx].Traces;
                    var lines = traces == null
                        ? Array.Empty<string>()
                        : traces.Select((t, step) => SerializeTraceStep(step, t)).ToArray();
                    await File.WriteAllLinesAsync(outTrace, lines).ConfigureAwait(false);
                    if (verbose) Console.WriteLine($"  trace:                wrote {lines.Length} steps for tx[{idx}] to {outTrace}");
                }
            }
            return r;
        }

        // JSONL opcode trace: one step per line with {pc, op, gas, gasCost, depth, stack}.
        // Memory/storage omitted to keep diff focused on control flow + value flow.
        // Depth in ProgramTrace is 0-indexed (0 = top-level frame); the standard
        // debug_traceTransaction format uses 1-indexed depth (1 = top-level call).
        // We bump by 1 here for direct comparability.
        private static string SerializeTraceStep(int step, ProgramTrace t)
        {
            var stack = t.Stack ?? new List<string>();
            var stackJson = "[" + string.Join(",", stack.Select(s =>
                "\"0x" + (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s).TrimStart('0') + "\"")) + "]";
            int pc = t.Instruction?.Step ?? -1;
            string op = t.Instruction?.Instruction?.ToString() ?? "UNKNOWN";
            return "{" +
                $"\"step\":{step}," +
                $"\"pc\":{pc}," +
                $"\"op\":\"{op}\"," +
                $"\"gas\":{t.GasRemaining}," +
                $"\"gasCost\":{t.GasCost}," +
                $"\"depth\":{t.Depth + 1}," +
                $"\"stack\":{stackJson}" +
                "}";
        }

        // === MainnetBlockFixture emitter ===
        // Produces a block-N.json file compatible with the
        // Debug_BlockReplay_DiffWithGethT8n harness in MainnetBlockReplayTests.
        // Pre-state for every touched account is fetched at block N-1 via RPC
        // in parallel (balance / nonce / code / storage[slot] for every touched slot).
        private static async Task EmitFixtureAsync(
            IEthApiService eth, Block rpcBlock, BlockHeader header,
            List<string> rawTxHexes, List<string> dirtyAddrs,
            List<(string addr, BigInteger slot)> slotPairs,
            long blockNumber, string outPath)
        {
            var parentBlockParam = new BlockParameter(new HexBigInteger(blockNumber - 1));

            var balanceTasks = dirtyAddrs.Select(a => eth.GetBalance.SendRequestAsync(a, parentBlockParam)).ToArray();
            var nonceTasks = dirtyAddrs.Select(a => eth.Transactions.GetTransactionCount.SendRequestAsync(a, parentBlockParam)).ToArray();
            var codeTasks = dirtyAddrs.Select(a => eth.GetCode.SendRequestAsync(a, parentBlockParam)).ToArray();
            var slotTasks = slotPairs.Select(p => eth.GetStorageAt.SendRequestAsync(p.addr, new HexBigInteger(p.slot), parentBlockParam)).ToArray();
            await Task.WhenAll(
                Task.WhenAll(balanceTasks),
                Task.WhenAll(nonceTasks),
                Task.WhenAll(codeTasks),
                Task.WhenAll(slotTasks)).ConfigureAwait(false);

            var preState = new Dictionary<string, object>(dirtyAddrs.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirtyAddrs.Count; i++)
            {
                var key = dirtyAddrs[i].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? dirtyAddrs[i].ToLowerInvariant()
                    : "0x" + dirtyAddrs[i].ToLowerInvariant();
                preState[key] = new
                {
                    balance = "0x" + balanceTasks[i].Result.Value.ToString("x"),
                    nonce = "0x" + nonceTasks[i].Result.Value.ToString("x"),
                    code = codeTasks[i].Result ?? "0x",
                    storage = new Dictionary<string, string>()
                };
            }

            for (int i = 0; i < slotPairs.Count; i++)
            {
                var addr = slotPairs[i].addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? slotPairs[i].addr.ToLowerInvariant()
                    : "0x" + slotPairs[i].addr.ToLowerInvariant();
                if (!preState.TryGetValue(addr, out var entry)) continue;
                var slotHex = "0x" + slotPairs[i].slot.ToString("x");
                var valueHex = slotTasks[i].Result ?? "0x";
                var storage = (Dictionary<string, string>)entry.GetType().GetProperty("storage").GetValue(entry);
                storage[slotHex] = valueHex;
            }

            var headerJson = new
            {
                parentHash = "0x" + (header.ParentHash?.ToHex() ?? ""),
                unclesHash = "0x" + (header.UnclesHash?.ToHex() ?? ""),
                coinbase = header.Coinbase ?? "",
                stateRoot = "0x" + (header.StateRoot?.ToHex() ?? ""),
                transactionsRoot = "0x" + (header.TransactionsHash?.ToHex() ?? ""),
                receiptsRoot = "0x" + (header.ReceiptHash?.ToHex() ?? ""),
                logsBloom = "0x" + (header.LogsBloom?.ToHex() ?? ""),
                difficulty = "0x" + header.Difficulty.ToBigInteger().ToString("x"),
                number = "0x" + header.BlockNumber.ToBigInteger().ToString("x"),
                gasLimit = "0x" + header.GasLimit.ToString("x"),
                gasUsed = "0x" + header.GasUsed.ToString("x"),
                timestamp = "0x" + header.Timestamp.ToString("x"),
                extraData = "0x" + (header.ExtraData?.ToHex() ?? ""),
                mixHash = "0x" + (header.MixHash?.ToHex() ?? ""),
                nonce = "0x" + (header.Nonce?.ToHex() ?? ""),
                baseFee = header.BaseFee.HasValue ? "0x" + header.BaseFee.Value.ToBigInteger().ToString("x") : null,
                withdrawalsRoot = header.WithdrawalsRoot != null ? "0x" + header.WithdrawalsRoot.ToHex() : null,
                parentBeaconBlockRoot = header.ParentBeaconBlockRoot != null ? "0x" + header.ParentBeaconBlockRoot.ToHex() : null
            };

            var fixture = new
            {
                blockNumber = blockNumber,
                scenario = "blockreplay-emit-fixture",
                header = headerJson,
                transactionsRlp = rawTxHexes,
                uncles = new object[0],
                preState = preState
            };

            var json = System.Text.Json.JsonSerializer.Serialize(fixture, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            });
            await File.WriteAllTextAsync(outPath, json).ConfigureAwait(false);
        }

        private static byte[] StripLeadingZeros(byte[] v)
        {
            if (v == null || v.Length == 0) return Array.Empty<byte>();
            int i = 0;
            while (i < v.Length && v[i] == 0) i++;
            if (i == v.Length) return Array.Empty<byte>();
            var ret = new byte[v.Length - i];
            Array.Copy(v, i, ret, 0, ret.Length);
            return ret;
        }

        private static void ApplyExtraHeaderFields(BlockHeader header, Block rpcBlock)
        {
            if (rpcBlock.WithdrawalsRoot != null && !string.IsNullOrEmpty(rpcBlock.WithdrawalsRoot.HexValue))
                header.WithdrawalsRoot = rpcBlock.WithdrawalsRoot.HexValue.HexToByteArray();
            if (!string.IsNullOrEmpty(rpcBlock.ParentBeaconBlockRoot))
                header.ParentBeaconBlockRoot = rpcBlock.ParentBeaconBlockRoot.HexToByteArray();
            if (rpcBlock.BlobGasUsed != null)
                header.BlobGasUsed = (long)rpcBlock.BlobGasUsed.Value;
            if (rpcBlock.ExcessBlobGas != null)
                header.ExcessBlobGas = (long)rpcBlock.ExcessBlobGas.Value;
            if (!string.IsNullOrEmpty(rpcBlock.RequestsHash))
                header.RequestsHash = rpcBlock.RequestsHash.HexToByteArray();
        }
    }
}
