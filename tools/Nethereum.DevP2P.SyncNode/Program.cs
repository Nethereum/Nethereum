using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.SyncNode;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Live mainnet from-genesis replay validator. Connects to a real Ethereum
    /// mainnet peer (Ethereum Foundation bootnode by default), pulls headers +
    /// bodies in batches, and replays each block through
    /// <see cref="BlockProcessor"/> with <see cref="MainnetChainActivations"/>.
    /// Per-block state-root match is reported; the process aborts on the first
    /// mismatch unless <c>--continue-on-mismatch</c> is passed.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] argv)
        {
            // Subcommand dispatch: 'monitor' runs the SyncMonitor external
            // observer instead of the sync follower. Kept as a sibling
            // subcommand so the same binary covers both sync + observe.
            if (argv.Length > 0 && string.Equals(argv[0], "monitor", StringComparison.OrdinalIgnoreCase))
            {
                var rest = new string[argv.Length - 1];
                Array.Copy(argv, 1, rest, 0, rest.Length);
                return await SyncMonitor.RunAsync(rest);
            }

            CliArgs args;
            try { args = CliArgs.Parse(argv); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 64; // EX_USAGE
            }
            if (args.ShowHelp) { CliArgs.PrintUsage(); return 0; }
            if (args.StartBlock == 0)
            {
                Console.Error.WriteLine("error: --start-block must be >= 1 (genesis is loaded directly)");
                return 64;
            }

            var swTotal = Stopwatch.StartNew();
            Banner(args);

            // Single shared LoggerFactory routes structured logs from PeerPoolManager,
            // FetchRequestScheduler, DevP2PBlockSource and FollowerService to the
            // Console with timestamps. Per-category filter levels can be tuned via
            // configuration if needed; defaults are Info+.
            using var loggerFactory = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Information);
                b.AddSimpleConsole(opts =>
                {
                    opts.SingleLine = true;
                    opts.TimestampFormat = "HH:mm:ss ";
                    opts.IncludeScopes = false;
                });
            });

            // Canonical-truth source for divergence diagnosis. Always
            // starts with the hardcoded MainnetKnownCheckpoints — offline,
            // unfalsifiable, instant verdict at the famous mainnet
            // milestones. If --canonical-rpc is also set the RPC source
            // composes behind for all other heights. Order matters: known
            // checkpoints answer first so a hostile RPC cannot mask a state
            // divergence at a famous block.
            var knownCheckpoints = new Nethereum.CoreChain.Validation.MainnetKnownCheckpoints();
            Console.WriteLine($"Canonical source: {knownCheckpoints.Name} (offline, {knownCheckpoints.Count} pinned blocks)");
            Nethereum.CoreChain.Validation.ICanonicalStateRootSource canonicalSource = knownCheckpoints;
            if (!string.IsNullOrWhiteSpace(args.CanonicalRpc))
            {
                try
                {
                    var rpcSource = new RpcCanonicalSource(args.CanonicalRpc);
                    canonicalSource = new Nethereum.CoreChain.Validation.CompositeCanonicalStateRootSource(
                        knownCheckpoints, rpcSource);
                    Console.WriteLine($"Canonical source: {canonicalSource.Name} (composed; consulted on state-root divergence)");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"warning: --canonical-rpc URL invalid ({ex.Message}); falling back to MainnetKnownCheckpoints only.");
                }
            }

            // 1. Open storage backend (RocksDB or in-memory) and seed genesis
            //    if needed. On resume from an existing RocksDB the alloc step
            //    is skipped — the canonical genesis state is already there.
            // Note: NOT `using var` — the auto-rewind path may dispose this
            //   and re-open it from a snapshot mid-run.
            // Journal config is captured once so the auto-rewind path can
            // re-open the backend with identical settings.
            var journalOptions = args.BuildJournalOptions();
            IChainStoreBundle bundle = args.DataDir != null
                ? RocksDbChainStoreBundle.Open(args.DataDir, journalOptions)
                : (IChainStoreBundle)InMemoryChainStoreBundle.Open(journalOptions);
            Console.WriteLine(args.DataDir != null
                ? $"Storage: RocksDB at {args.DataDir}"
                : "Storage: in-memory (volatile)");
            Console.WriteLine(bundle.JournalEnabled
                ? $"Journal: enabled, retention {args.JournalBlocks:N0} blocks"
                : "Journal: disabled (--journal-blocks 0)");

            if (args.RewindToCheckpoint.HasValue)
            {
                if (args.DataDir == null)
                {
                    Console.Error.WriteLine("error: --rewind-to-checkpoint requires --data-dir (no checkpoints in in-memory mode).");
                    return 64;
                }
                ulong target = args.RewindToCheckpoint.Value;
                var nearest = bundle.Metadata.GetNearestCheckpointAtOrBefore(target);
                if (nearest == null)
                {
                    Console.Error.WriteLine(
                        $"error: no checkpoint at or below block {target:N0} in {args.DataDir}. " +
                        "Run a sync with --checkpoint-every N first.");
                    return 65;
                }
                ulong oldLast = bundle.Metadata.GetLastBlock();
                ulong cpBlock = nearest.Value.BlockNumber;
                byte[] cpStateRoot = nearest.Value.StateRoot;
                byte[] cpBlockHash = nearest.Value.BlockHash;
                var rocksBundle = bundle as RocksDbChainStoreBundle
                    ?? throw new InvalidOperationException("--rewind-to-checkpoint requires RocksDB bundle.");
                var snapshotDir = rocksBundle.ResolveCheckpointSnapshotPath(cpBlock);

                if (!System.IO.Directory.Exists(snapshotDir))
                {
                    Console.Error.WriteLine(
                        $"error: checkpoint metadata records block {cpBlock:N0} but snapshot dir {snapshotDir} is missing.");
                    return 65;
                }

                // Close the bundle BEFORE restoring — RestoreFromCheckpointDir
                // swaps SST files in --data-dir and must not race with an
                // open RocksDB backend.
                await bundle.DisposeAsync().ConfigureAwait(false);

                RocksDbChainStoreBundle.RestoreFromCheckpointDir(snapshotDir, args.DataDir);

                // Re-open the bundle on the restored state and set the cursor
                // directly to the snapshot's block number. The snapshot's
                // metadata column family is missing its own checkpoint row
                // (the "self-row gap" — SaveCheckpoint snapshots BEFORE writing
                // its row), so we re-write both the checkpoint row and the
                // last_block cursor here. RewindToCheckpointAtOrBefore would
                // otherwise pick the next-older checkpoint instead.
                bundle = RocksDbChainStoreBundle.Open(args.DataDir, journalOptions);
                bundle.Metadata.SaveCheckpoint(cpBlock, cpStateRoot, cpBlockHash);
                bundle.Metadata.Commit(cpBlock, cpBlockHash);
                if (bundle.JournalEnabled)
                    await bundle.Diffs.DeleteDiffsAboveBlockAsync(cpBlock).ConfigureAwait(false);
                Console.WriteLine(
                    $"Rewound from block {oldLast:N0} to checkpoint {cpBlock:N0} " +
                    $"(state restored from {snapshotDir}, state root 0x{cpStateRoot.ToHex().Substring(0, 16)}…). " +
                    $"Next sync resumes from block {cpBlock + 1:N0}.");
                await bundle.DisposeAsync().ConfigureAwait(false);
                return 0;
            }

            if (args.SnapshotCheckpoint.HasValue)
            {
                if (args.DataDir == null)
                {
                    Console.Error.WriteLine("error: --snapshot-checkpoint requires --data-dir (source must be RocksDB).");
                    return 64;
                }
                if (string.IsNullOrEmpty(args.SnapshotOutput))
                {
                    Console.Error.WriteLine("error: --snapshot-checkpoint requires --snapshot-output PATH.");
                    return 64;
                }
                if (System.IO.Directory.Exists(args.SnapshotOutput))
                {
                    Console.Error.WriteLine($"error: --snapshot-output path already exists: {args.SnapshotOutput}");
                    return 64;
                }
                ulong target = args.SnapshotCheckpoint.Value;
                var nearestSrc = bundle.Metadata.GetNearestCheckpointAtOrBefore(target);
                if (nearestSrc == null)
                {
                    Console.Error.WriteLine(
                        $"error: no checkpoint at or below block {target:N0} in {args.DataDir}. " +
                        "Run a sync with --checkpoint-every N first.");
                    return 65;
                }
                Console.WriteLine($"Creating hard-linked snapshot of {args.DataDir} at {args.SnapshotOutput} …");
                var snapSw = Stopwatch.StartNew();
                await bundle.ExportDatabaseAsync(args.SnapshotOutput);
                snapSw.Stop();
                Console.WriteLine($"  snapshot done ({snapSw.ElapsedMilliseconds} ms).");
                // Now open the snapshot and rewind its metadata to the checkpoint
                // so a subsequent --data-dir <snapshot> run resumes from there.
                bundle.Dispose();
                using var snap = RocksDbChainStoreBundle.Open(args.SnapshotOutput);
                var cp = snap.Metadata.RewindToCheckpointAtOrBefore(target);
                Console.WriteLine(
                    $"Snapshot rewound to checkpoint at block {cp.BlockNumber:N0} " +
                    $"(state root 0x{cp.StateRoot.ToHex().Substring(0, 16)}…). " +
                    $"Audit replay: nethereum-syncnode --data-dir {args.SnapshotOutput} --start-block {cp.BlockNumber + 1:N0} --blocks K --debug-accounts …");
                return 0;
            }

            if (args.PruneCheckpointsKeepEvery.HasValue)
            {
                if (args.DataDir == null)
                {
                    Console.Error.WriteLine("error: --prune-checkpoints requires --data-dir.");
                    return 64;
                }
                ulong keepEvery = args.PruneCheckpointsKeepEvery.Value;
                if (keepEvery == 0)
                {
                    Console.Error.WriteLine("error: --prune-checkpoints N must be > 0.");
                    return 64;
                }
                var checkpoints = bundle.Metadata.ListCheckpointBlockNumbers();
                var snapDir = System.IO.Path.Combine(args.DataDir, ".cp");
                int kept = 0, dropped = 0;
                long freedBytes = 0;
                long totalBeforeBytes = System.IO.Directory.Exists(snapDir)
                    ? DirectorySize(snapDir) : 0;
                Console.WriteLine($"Pruning checkpoints in {args.DataDir} — keep multiples of {keepEvery:N0}.");
                Console.WriteLine($"  found {checkpoints.Count} checkpoints; total snapshot dir size = {FormatBytes(totalBeforeBytes)}.");
                foreach (var bn in checkpoints)
                {
                    if (bn % keepEvery == 0 && bn > 0)
                    {
                        kept++;
                        continue;
                    }
                    await bundle.DeleteCheckpointAsync(bn);
                    var snapPath = System.IO.Path.Combine(snapDir, bn.ToString("D12"));
                    if (System.IO.Directory.Exists(snapPath))
                    {
                        long size = DirectorySize(snapPath);
                        try
                        {
                            System.IO.Directory.Delete(snapPath, recursive: true);
                            freedBytes += size;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"  warning: could not delete {snapPath}: {ex.Message}");
                        }
                    }
                    dropped++;
                }
                Console.WriteLine($"  kept {kept}, dropped {dropped}, freed {FormatBytes(freedBytes)}.");
                if (kept > 0)
                {
                    Console.WriteLine($"  remaining checkpoints: {string.Join(", ", bundle.Metadata.ListCheckpointBlockNumbers())}");
                }
                return 0;
            }

            if (args.ResetState)
            {
                if (args.DataDir == null)
                {
                    Console.Error.WriteLine("error: --reset-state requires --data-dir (in-memory mode has no persistent state).");
                    return 64;
                }
                Console.Write("Reset-state: wiping state trie + receipts + logs + checkpoints, keeping headers + txs + uncles … ");
                var resetSw = Stopwatch.StartNew();
                await bundle.ResetStateOnlyAsync();
                resetSw.Stop();
                Console.WriteLine($"done ({resetSw.ElapsedMilliseconds} ms).");
                // Also remove the audit snapshot dir (.cp/) — its contents are
                // hard-linked RocksDB checkpoints, all derived from the now-wiped
                // state trie. Keep it would leave dangling references.
                var snapDir = System.IO.Path.Combine(args.DataDir, ".cp");
                if (System.IO.Directory.Exists(snapDir))
                {
                    try
                    {
                        System.IO.Directory.Delete(snapDir, recursive: true);
                        Console.WriteLine($"  audit snapshot dir {snapDir} removed.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  warning: could not remove {snapDir}: {ex.Message}");
                    }
                }
                Console.WriteLine("Next run with --re-execute-from 1 will rebuild state from the local chain data on the current EVM.");
                return 0;
            }

            bool freshLoad = !bundle.Metadata.IsGenesisLoaded();
            if (freshLoad)
            {
                Console.Write("Loading mainnet genesis allocation … ");
                var genesisSw = Stopwatch.StartNew();
                var count = await MainnetGenesisLoader.PopulateAsync(bundle.State);
                genesisSw.Stop();
                Console.WriteLine($"done. {count} accounts ({genesisSw.ElapsedMilliseconds} ms).");
                bundle.Metadata.MarkGenesisLoaded();
            }
            else
            {
                Console.WriteLine("Genesis already loaded in store (skipping alloc).");
            }

            // 2. Build the canonical BlockExecutor engine + BlockImporter wrapper.
            // Always wrap state in FixturePreStateRecorder — it's a pure
            // pass-through when IsRecording=false, so the only cost on the
            // non-fixture sync path is one extra virtual call per read. We
            // toggle IsRecording per-block around the target fixture blocks
            // so the same IncrementalStateRootCalculator and engine instance
            // stay in use across the whole sync (preserving dirty-tracking
            // continuity that the calculator depends on).
            var recordingState = new FixturePreStateRecorder(bundle.State);
            var calculator = new Nethereum.CoreChain.IncrementalStateRootCalculator(recordingState, bundle.TrieNodes);
            var engine = new Nethereum.CoreChain.BlockExecutor(
                recordingState,
                bundle.Blocks,
                MainnetChainActivations.Instance,
                chainConfigFactory: f => new Nethereum.CoreChain.ChainConfig
                {
                    ChainId = MainnetGenesisConstants.ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Hardfork = f.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: f => DefaultMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calculator,
                rewardPolicy: Nethereum.CoreChain.EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: bundle.TrieNodes);
            var processor = new Nethereum.CoreChain.BlockImporter(
                engine,
                bundle.Blocks,
                recordingState,
                bundle.Transactions,
                bundle.Receipts,
                bundle.Logs,
                bundle.Uncles);

            // On a fresh load, verify the genesis allocation hashes to the
            // canonical mainnet pin. On resume, the calculator's "current root"
            // is the post-last-block root (not genesis), so the check would be
            // wrong; we trust the persisted state implicitly and let the next
            // block's parent-hash + state-root check catch any divergence.
            if (freshLoad)
            {
                var computedGenesisRoot = await calculator.ComputeStateRootAsync();
                if (!ByteUtil.AreEqual(computedGenesisRoot, MainnetGenesisConstants.StateRootHex.HexToByteArray()))
                {
                    Console.Error.WriteLine(
                        $"FATAL: computed genesis state root {computedGenesisRoot.ToHex()} does not match canonical " +
                        $"{MainnetGenesisConstants.StateRootHex} — alloc fixture corrupted.");
                    return 70; // EX_SOFTWARE
                }
                Console.WriteLine($"Genesis state root verified: 0x{computedGenesisRoot.ToHex()}");
            }

            // Re-execute mode: replay locally-stored chain data (headers + txs
            // + uncles) through BlockProcessor without dialling any peers. The
            // backing assumption is that headers/bodies in IBlockStore +
            // ITransactionStore + IUncleStore are byte-canonical (came from
            // peers during a previous sync). State is rebuilt on the current
            // EVM rules, so this is the fast-iteration path to verify an EVM
            // fix end-to-end without re-fetching anything from the network.
            // Time-travel: any target block N produces canonical state at N.
            if (args.ReExecuteFrom.HasValue)
            {
                if (args.DataDir == null)
                {
                    Console.Error.WriteLine("error: --re-execute-from requires --data-dir (chain data must be persistent).");
                    return 64;
                }
                ulong fromBlock = args.ReExecuteFrom.Value;
                ulong storedHead = (ulong)await bundle.Blocks.GetHeightAsync();
                ulong toBlock = args.ReExecuteTo ?? storedHead;
                if (toBlock > storedHead)
                {
                    Console.Error.WriteLine($"error: --re-execute-to {toBlock:N0} exceeds stored chain head {storedHead:N0}. Sync more headers first.");
                    return 64;
                }
                if (fromBlock == 0 || fromBlock > toBlock)
                {
                    Console.Error.WriteLine($"error: invalid range {fromBlock:N0}..{toBlock:N0}.");
                    return 64;
                }
                ulong dbLast = bundle.Metadata.GetLastBlock();
                if (fromBlock != dbLast + 1)
                {
                    Console.Error.WriteLine(
                        $"error: --re-execute-from {fromBlock:N0} must equal last_block ({dbLast:N0}) + 1. " +
                        "Use --reset-state to start at block 1, or first rebuild state up to {fromBlock-1} another way.");
                    return 64;
                }

                Console.WriteLine($"Re-execute mode: replaying blocks {fromBlock:N0}..{toBlock:N0} from local store (no network).");
                using var reCts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    if (!reCts.IsCancellationRequested)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Ctrl-C received — stopping re-execute.");
                        e.Cancel = true;
                        reCts.Cancel();
                    }
                };

                var reSw = Stopwatch.StartNew();
                ulong reported = 0;
                byte[] lastHash = null;
                for (ulong block = fromBlock; block <= toBlock && !reCts.IsCancellationRequested; block++)
                {
                    var header = await bundle.Blocks.GetByNumberAsync(block).ConfigureAwait(false);
                    if (header == null)
                    {
                        Console.Error.WriteLine($"  ERROR: header for block {block:N0} not in store. Cannot re-execute past gap.");
                        return 75;
                    }
                    var blockHash = await bundle.Blocks.GetHashByNumberAsync(block).ConfigureAwait(false);
                    var txs = await bundle.Transactions.GetByBlockHashAsync(blockHash).ConfigureAwait(false)
                              ?? new List<Nethereum.Model.ISignedTransaction>();
                    var uncles = await bundle.Uncles.GetByBlockHashAsync(blockHash).ConfigureAwait(false)
                                 ?? new List<BlockHeader>();
                    var castTxs = txs.Cast<Nethereum.Model.ISignedTransaction>().ToList();
                    var castUncles = uncles.ToList();

                    var result = await processor.ImportAsync(
                        header, castTxs, castUncles, withdrawals: null, ct: reCts.Token).ConfigureAwait(false);

                    if (result.Exception != null || result.StateRootMismatch)
                    {
                        Console.Error.WriteLine($"  STATE-ROOT MISMATCH at block {block:N0}: {result.ErrorMessage ?? "computed root mismatch"}");
                        Console.Error.WriteLine($"  expected 0x{result.ExpectedStateRoot.ToHex()}, computed 0x{result.ComputedStateRoot.ToHex()}");
                        if (!args.ContinueOnMismatch) return 76;
                    }

                    lastHash = blockHash;
                    if (args.FlushEvery > 0 && (block - fromBlock + 1) % (ulong)args.FlushEvery == 0)
                    {
                        bundle.Metadata.Commit(block, blockHash);
                    }
                    if (bundle is RocksDbChainStoreBundle
                        && args.CheckpointEvery > 0 && block > 0
                        && block % args.CheckpointEvery == 0
                        && result.ComputedStateRoot != null)
                    {
                        try
                        {
                            var cp = await bundle.SaveCheckpointAsync(block, result.ComputedStateRoot, blockHash);
                            Console.WriteLine(
                                $"[checkpoint {cp.BlockNumber:N0}] stateRoot=0x{cp.StateRoot.ToHex()[..16]}…  blockHash=0x{cp.BlockHash.ToHex()[..16]}…  saved -> {bundle.ResolveCheckpointSnapshotPath(cp.BlockNumber)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[checkpoint {block:N0}] FAILED: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                    if (args.ReportEvery > 0 && (block - reported) >= (ulong)args.ReportEvery)
                    {
                        double bps = (block - fromBlock + 1) / Math.Max(0.001, reSw.Elapsed.TotalSeconds);
                        Console.WriteLine($"  re-executed up to {block:N0} ({bps:F0} blk/s, {reSw.Elapsed.TotalSeconds:F1}s elapsed)");
                        reported = block;
                    }
                }
                // Final commit so a graceful exit persists last_block.
                if (lastHash != null) bundle.Metadata.Commit(toBlock, lastHash);
                reSw.Stop();
                Console.WriteLine($"Re-execute complete: {fromBlock:N0}..{toBlock:N0} in {reSw.Elapsed.TotalSeconds:F1}s.");
                return 0;
            }

            // Resume support: if the store remembers a higher block than --start-block,
            // advance to (last_block + 1) automatically and use the remembered hash as parent.
            var resumeBlock = bundle.Metadata.GetLastBlock();
            var resumeHash = bundle.Metadata.GetLastBlockHash();
            byte[] parentHash;
            ulong effectiveStart;
            if (resumeBlock > 0 && resumeHash != null && resumeBlock + 1 >= args.StartBlock)
            {
                effectiveStart = resumeBlock + 1;
                parentHash = resumeHash;
                Console.WriteLine($"Resuming from block {effectiveStart:N0} (parent 0x{resumeHash.ToHex()[..16]}…)");
            }
            else
            {
                effectiveStart = args.StartBlock;
                parentHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            }

            // 3. Sync via the new Stage 6 architecture: PeerPoolManager +
            //    FetchRequestScheduler + DevP2PBlockSource + MainnetChainNode.
            //    The follower service inside MainnetChainNode handles batch
            //    fetching, executor wiring, divergence detection, journal
            //    rewind, and reports back via FollowerRunResult. Snapshot-
            //    restore comes back to us as SnapshotRestoreRequested; we
            //    loop, dispose, restore, rebuild, re-run.
            using var ctsRoot = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                if (!ctsRoot.IsCancellationRequested)
                {
                    Console.WriteLine();
                    Console.WriteLine("Ctrl-C received — stopping at the next safe point.");
                    e.Cancel = true;
                    ctsRoot.Cancel();
                }
            };

            string[] dialPool = args.Peer != null ? new[] { args.Peer } : MainnetPeerSession.MainnetBootnodes;
            Nethereum.DevP2P.NodeDb.PersistentPeerCache peerCache = null;
            if (!string.IsNullOrEmpty(args.PeerCachePath)
                && !string.Equals(args.PeerCachePath, "off", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args.PeerCachePath, "-", StringComparison.OrdinalIgnoreCase))
            {
                peerCache = new Nethereum.DevP2P.NodeDb.PersistentPeerCache(args.PeerCachePath, Console.WriteLine);
                peerCache.Load();
            }

            await using var pool = new Nethereum.DevP2P.Sync.PeerPoolManager(
                new Nethereum.DevP2P.Sync.MainnetPeerHandshakeWorker(),
                new Nethereum.DevP2P.Sync.PeerPoolOptions(
                    TargetPeerCount: args.TargetPeers,
                    MaxConcurrentDials: 10,
                    MinPeerLatestBlock: resumeBlock + 1),
                bootnodes: dialPool,
                logger: loggerFactory.CreateLogger<Nethereum.DevP2P.Sync.PeerPoolManager>(),
                peerCache: peerCache);
            await pool.StartAsync(ctsRoot.Token);

            // Seed the pool from EIP-1459 mainnet DNS trees (all/snap/les.mainnet.ethdisco.net).
            // Resolves ~5,000+ enodes; runs once at startup in the background.
            _ = Task.Run(async () =>
            {
                try
                {
                    var dnsResolver = new Nethereum.DevP2P.Dns.EnrTreeResolver(Console.WriteLine);
                    foreach (var tree in Nethereum.DevP2P.Dns.EnrTreeResolver.MainnetEnrTrees)
                    {
                        var enodes = await dnsResolver.ResolveAsync(
                            tree, TimeSpan.FromSeconds(30), 5000, ctsRoot.Token);
                        foreach (var enode in enodes)
                            pool.EnqueueCandidate(enode);
                        Console.WriteLine($"DNS tree {tree.Split('@')[1]} → {enodes.Count} enodes enqueued.");
                    }
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex) { Console.WriteLine($"DNS seeding failed: {ex.GetType().Name}: {ex.Message}"); }
            }, ctsRoot.Token);

            // Pool-starvation recovery: when the active pool drops below half
            // the target AND the candidate queue has run dry (manifesting as
            // "all tried peers exhausted" log lines), re-resolve the DNS
            // trees and re-prime from the persistent peer cache. Without
            // this the dial loop would block on WaitToReadAsync indefinitely
            // when both sources of fresh candidates are exhausted — exactly
            // the stall observed in long sessions where every initially-
            // seeded enode has churned through its 35s cooldown without a
            // single survivor.
            _ = Task.Run(async () =>
            {
                var starvationCheckInterval = TimeSpan.FromMinutes(2);
                var starvationThreshold = Math.Max(2, args.TargetPeers / 2);
                while (!ctsRoot.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(starvationCheckInterval, ctsRoot.Token).ConfigureAwait(false);
                        if (pool.ActivePeers.Count >= starvationThreshold) continue;

                        Console.WriteLine($"pool starvation detected (active={pool.ActivePeers.Count} threshold={starvationThreshold}) — reseeding from DNS + peer cache");

                        if (peerCache is not null)
                        {
                            int reprimed = 0;
                            foreach (var enode in peerCache.GetPreferredEnodes(256))
                            {
                                pool.EnqueueCandidate(enode);
                                reprimed++;
                            }
                            if (reprimed > 0)
                                Console.WriteLine($"  peer cache reprimed {reprimed} enodes");
                        }

                        var dnsResolver = new Nethereum.DevP2P.Dns.EnrTreeResolver(Console.WriteLine);
                        foreach (var tree in Nethereum.DevP2P.Dns.EnrTreeResolver.MainnetEnrTrees)
                        {
                            try
                            {
                                var enodes = await dnsResolver.ResolveAsync(
                                    tree, TimeSpan.FromSeconds(20), 2000, ctsRoot.Token);
                                foreach (var enode in enodes)
                                    pool.EnqueueCandidate(enode);
                                Console.WriteLine($"  DNS tree {tree.Split('@')[1]} → {enodes.Count} enodes reseeded");
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception treeEx)
                            {
                                Console.WriteLine($"  DNS tree {tree.Split('@')[1]} reseed failed: {treeEx.GetType().Name}: {treeEx.Message}");
                            }
                        }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"reseed loop error: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }, ctsRoot.Token);

            var scheduler = new Nethereum.DevP2P.Sync.FetchRequestScheduler(
                pool,
                new Nethereum.DevP2P.Sync.MainnetPeerRequestWorker(),
                new Nethereum.DevP2P.Sync.FetchRequestSchedulerOptions(),
                pool.GetScore,
                loggerFactory.CreateLogger<Nethereum.DevP2P.Sync.FetchRequestScheduler>());

            // Open an RLPx listener that accepts inbound peers and serves
            // eth/68 read requests from our backend stores (#135). Helps with
            // mainnet peer scarcity (we dial 0.3% success; passively accepting
            // gives us peers that would otherwise reject our dial). Also
            // exercises the per-IP throttle + MaxPeers + per-subnet caps +
            // trusted-peer admission (#145/#146/#181/#183) and NetRestrict CIDR
            // (#148). Composition lives in Nethereum.DevP2P.Sync.PeerListener
            // so AppChain validators and mainnet relays can wire the same code
            // without duplicating it.
            //
            // --listen-port semantics: N > 0 = bind that TCP port,
            // N = 0 = OS-chosen ephemeral, N < 0 = disable inbound serving.
            // Default 30303 is the well-known Ethereum p2p TCP port.
            Nethereum.DevP2P.Sync.PeerListener peerListener = null;
            Nethereum.Signer.EthECKey listenerKey = null;
            if (args.ListenPort >= 0)
            {
                listenerKey = Nethereum.Signer.EthECKey.GenerateKey();
                var statusTemplate = BuildLocalStatusTemplate(bundle);
                var listenerOptions = new Nethereum.DevP2P.Sync.PeerListenerOptions
                {
                    ListenPort = args.ListenPort,
                    BindAddress = System.Net.IPAddress.Any,
                    MaxInboundPeers = 25,
                    MaxInboundPerIP = 9,
                    HandshakeTimeoutMs = 10000,
                    ServeSnap = false,
                    // Serve-empty mode (#217): when on (default), mirror the
                    // remote peer's Status so an inbound peer at any
                    // chain head is admitted — they fall back to using us for
                    // old blocks only and silent-skip (#219) handles requests
                    // above our cursor. When off, assert our own Status so a
                    // peer expecting a specific chain head can detect the gap
                    // and reject our Status reply.
                    MirrorRemoteStatus = args.ServeEmpty,
                    ClientId = "Nethereum/sync-node",
                    // Forward inbound-peer lifecycle to a dial scheduler when
                    // one is in use (geth p2p/dial.go ratio cap). PeerPoolManager
                    // does not currently instantiate one in SyncNode, so the
                    // hooks land as no-ops today; AppChain validators wiring a
                    // scheduler get accurate inbound counts without further
                    // PeerListener changes.
                    OnInboundPeerAdded = key =>
                        loggerFactory.CreateLogger("InboundLifecycle").LogDebug(
                            "Inbound peer added key={Key}", key),
                    OnInboundPeerRemoved = key =>
                        loggerFactory.CreateLogger("InboundLifecycle").LogDebug(
                            "Inbound peer removed key={Key}", key),
                };
                peerListener = new Nethereum.DevP2P.Sync.PeerListener(
                    listenerKey, bundle, listenerOptions, statusTemplate,
                    logger: loggerFactory.CreateLogger<Nethereum.DevP2P.Sync.PeerListener>());
                await peerListener.StartAsync(ctsRoot.Token);
                Console.WriteLine($"Inbound RLPx listener active on 0.0.0.0:{peerListener.Port} " +
                                  $"(NodeId 0x{listenerKey.GetPubKeyNoPrefix().ToHex().Substring(0, 16)}… " +
                                  $"serveEmpty={args.ServeEmpty})");
            }
            else
            {
                Console.WriteLine("Inbound RLPx listener disabled (--listen-port < 0; client-only).");
            }

            // discv5 is the third peer-discovery source alongside discv4
            // bootnodes (MainnetPeerSession.MainnetBootnodes) and EIP-1459
            // DNS trees (EnrTreeResolver). Enabled by default — set
            // --disable-discv5 to opt out. The listener binds an ephemeral
            // UDP port unless --discv5-port specifies one; the bootnode bond
            // + FINDNODE walk runs through Discv5PeerDiscoveryService and
            // surfaces every discovered ENR as an enode URL on the same
            // candidate channel as the other two sources.
            Nethereum.DevP2P.Discv5.Discv5Listener discv5 = null;
            if (!args.DisableDiscv5)
            {
                var discv5Key = Nethereum.Signer.EthECKey.GenerateKey();
                discv5 = new Nethereum.DevP2P.Discv5.Discv5Listener(discv5Key);
                discv5.Start(System.Net.IPAddress.Any, port: args.Discv5Port);

                // ENR must advertise the actually-bound UDP port; when
                // --discv5-port is 0 (default) the OS picks the port and
                // `discv5.Port` is the source of truth. Also advertise the
                // RlpxListener TCP port (#135) so peers walking our ENR via
                // discv5 know where to dial inbound. Omit `tcp` when the
                // listener is disabled (--listen-port < 0) — peers will then
                // treat us as client-only and not attempt a TCP dial.
                var localEnr = new Nethereum.Model.Enr.EnrRecord { Sequence = 1 };
                localEnr.Pairs["id"] = new byte[] { (byte)'v', (byte)'4' };
                localEnr.Pairs["ip"] = System.Net.IPAddress.Any.GetAddressBytes();
                localEnr.Pairs["udp"] = new byte[] { (byte)((discv5.Port >> 8) & 0xff), (byte)(discv5.Port & 0xff) };
                if (peerListener != null && peerListener.Port > 0)
                {
                    int tcpPort = peerListener.Port;
                    localEnr.Pairs["tcp"] = new byte[] { (byte)((tcpPort >> 8) & 0xff), (byte)(tcpPort & 0xff) };
                }
                Nethereum.Signer.Enr.EnrRecordSigner.Sign(localEnr, discv5Key);
                discv5.LocalEnrEncoded = Nethereum.Model.Enr.EnrRecordEncoder.EncodeRecord(localEnr);
                discv5.LocalEnrSequence = localEnr.Sequence;
                Console.WriteLine($"Inbound discv5 listener active on 0.0.0.0:{discv5.Port} " +
                                  $"(NodeId 0x{discv5.NodeId.ToHex().Substring(0, 16)}…" +
                                  (peerListener != null && peerListener.Port > 0
                                      ? $" tcp={peerListener.Port})"
                                      : ")"));

                // Background DNS seed for the discv5 routing table. ResolveEnrsAsync
                // returns the parsed EnrRecord (rather than enode:// strings) which
                // is exactly what Discv5RoutingTable.Entry expects via EnrEncoded.
                var discv5Ref = discv5;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var dnsResolver = new Nethereum.DevP2P.Dns.EnrTreeResolver(Console.WriteLine);
                        var enrs = await dnsResolver.ResolveEnrsAsync(
                            Nethereum.DevP2P.Dns.EnrTreeResolver.MainnetEnrTrees[0],
                            TimeSpan.FromSeconds(30), maxLeaves: 200, ctsRoot.Token).ConfigureAwait(false);
                        int upserted = 0;
                        foreach (var enr in enrs)
                        {
                            if (enr.Id != "v4") continue;
                            if (enr.Secp256k1 == null || enr.Secp256k1.Length != 33) continue;
                            var ip = enr.IP4;
                            if (ip == null) continue;
                            var udpPort = enr.UdpPort;
                            if (udpPort == null || udpPort.Value == 0) continue;

                            var nodeId = Nethereum.DevP2P.Discv5.Discv5Crypto.ComputeNodeId(enr.Secp256k1);
                            discv5Ref.Routing.Upsert(new Nethereum.DevP2P.Discv5.Discv5RoutingTable.Entry
                            {
                                NodeId = nodeId,
                                Address = new System.Net.IPEndPoint(ip, udpPort.Value),
                                EnrEncoded = Nethereum.Model.Enr.EnrRecordEncoder.EncodeRecord(enr),
                            });
                            upserted++;
                        }
                        Console.WriteLine($"discv5 DNS seed: upserted {upserted} routing-table entries " +
                                          $"from {enrs.Count} resolved ENRs.");
                    }
                    catch (OperationCanceledException) { /* shutdown */ }
                    catch (Exception ex) { Console.WriteLine($"discv5 DNS seed failed: {ex.GetType().Name}: {ex.Message}"); }
                }, ctsRoot.Token);
            }

            // Discv5 peer-discovery orchestrator — bonds with mainnet bootnodes
            // and walks their routing tables, surfacing every discovered ENR as
            // an enode URL into the peer pool's dial queue. Without this the
            // passive discv5 listener just sits there waiting for inbound packets.
            Nethereum.DevP2P.Discv5.Discv5PeerDiscoveryService discv5Discovery = null;
            if (discv5 != null)
            {
                var bootnodes = new System.Collections.Generic.List<(Nethereum.Model.Enr.EnrRecord, System.Net.IPEndPoint)>(
                    Nethereum.DevP2P.Discv5.Discv5Bootnodes.ResolveMainnet());
                discv5Discovery = new Nethereum.DevP2P.Discv5.Discv5PeerDiscoveryService(
                    discv5,
                    enode => pool.EnqueueCandidate(enode),
                    bootnodes,
                    Console.WriteLine);
                await discv5Discovery.StartAsync(ctsRoot.Token);
                Console.WriteLine($"Discv5 peer-discovery active: {bootnodes.Count} bootnodes, " +
                                  $"walk-interval={Nethereum.DevP2P.Discv5.Discv5PeerDiscoveryService.DefaultWalkInterval.TotalSeconds:F0}s.");
            }


            Console.WriteLine();
            ulong endBlock = effectiveStart + args.Blocks - 1;
            Console.WriteLine($"Following blocks {effectiveStart:N0} .. {endBlock:N0} ({args.Blocks:N0} blocks total)");
            Console.WriteLine(new string('-', 80));

            // Stage 6: Build MainnetChainNode and run the follower loop.
            // The follower handles batched fetch, parent-hash validation,
            // execution, state-root checking, journal rewind, and reports
            // back via FollowerRunResult. Snapshot restore comes back as
            // SnapshotRestoreRequested; we dispose+restore+rebuild and
            // re-invoke.
            var chainConfig = new Nethereum.CoreChain.ChainConfig
            {
                ChainId = MainnetGenesisConstants.ChainId,
                BaseFee = System.Numerics.BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = "cancun",
            };
            var hardforkConfig = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance
                .Get(Nethereum.EVM.HardforkName.Cancun);
            var txVerifier = new Nethereum.Signer.TransactionVerificationAndRecoveryImp();
            var canonical = canonicalSource;

            Nethereum.CoreChain.MainnetChainNode BuildNode(IChainStoreBundle currentBundle)
            {
                var source = new Nethereum.DevP2P.Sync.DevP2PBlockSource(
                    pool, scheduler,
                    parentHashLookup: async bn => bn == 0
                        ? null
                        : await currentBundle.Blocks.GetHashByNumberAsync((System.Numerics.BigInteger)(bn - 1)).ConfigureAwait(false),
                    headerBatchSize: args.HeadersBatch,
                    bodyBatchSize: args.BodiesBatch,
                    logger: loggerFactory.CreateLogger<Nethereum.DevP2P.Sync.DevP2PBlockSource>());

                var txProcessor = new Nethereum.CoreChain.TransactionProcessor(
                    currentBundle.State, currentBundle.Blocks, chainConfig, txVerifier, hardforkConfig);

                Func<IChainStoreBundle, Nethereum.CoreChain.Sync.IBlockExecutor> executorFactory = b =>
                {
                    var recordingState = new FixturePreStateRecorder(b.State);
                    var calc = new Nethereum.CoreChain.IncrementalStateRootCalculator(recordingState, b.TrieNodes);
                    var engine = new Nethereum.CoreChain.BlockExecutor(
                        recordingState, b.Blocks, MainnetChainActivations.Instance,
                        chainConfigFactory: f => new Nethereum.CoreChain.ChainConfig
                        {
                            ChainId = MainnetGenesisConstants.ChainId,
                            BaseFee = System.Numerics.BigInteger.Zero,
                            Coinbase = AddressUtil.ZERO_ADDRESS,
                            Hardfork = f.ToString().ToLowerInvariant()
                        },
                        hardforkConfigFactory: f => Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get(f),
                        stateRootCalculator: calc,
                        rewardPolicy: Nethereum.CoreChain.EthereumProofOfWorkRewardPolicy.Instance,
                        trieNodeStore: b.TrieNodes);
                    Nethereum.CoreChain.Sync.IBlockExecutor inner = new Nethereum.CoreChain.BlockImporter(
                        engine, b.Blocks, recordingState,
                        b.Transactions, b.Receipts, b.Logs, b.Uncles);

                    // Auto-fixture: when --dump-fixture-output is set, emit a
                    // regression-cell fixture for any block in --dump-fixture-blocks
                    // AND for any block whose state root mismatches. Pass-through
                    // when --dump-fixture-output is unset.
                    if (!string.IsNullOrEmpty(args.DumpFixtureOutputDir))
                    {
                        inner = new FixtureEmittingBlockExecutor(
                            inner,
                            recordingState,
                            b.State,
                            args.DumpFixtureBlocks,
                            args.DumpFixtureOutputDir,
                            Console.WriteLine);
                    }

                    // Outermost decorator: per-block progress lines + mismatch
                    // callout. Operator needs this to monitor million-block syncs.
                    inner = new ProgressReportingBlockExecutor(
                        inner,
                        pool,
                        args.ReportEvery,
                        loggerFactory.CreateLogger<ProgressReportingBlockExecutor>());

                    // Long-run sweep checkpoints + status file for the external
                    // SyncMonitor. Supplements ProgressReportingBlockExecutor:
                    // a wider milestone cadence and a stable, machine-readable
                    // status file for out-of-process tooling.
                    string statusFile = args.DataDir != null
                        ? System.IO.Path.Combine(args.DataDir, "sync-status.json")
                        : null;
                    inner = new ValidationReporter(
                        inner, pool,
                        milestoneEvery: 25_000,
                        statusFilePath: statusFile,
                        logger: loggerFactory.CreateLogger<ValidationReporter>());

                    return inner;
                };

                return new Nethereum.CoreChain.MainnetChainNode(
                    bundle: currentBundle,
                    source: source,
                    executorFactory: executorFactory,
                    policy: new ProductionValidationPolicy(
                        args.ContinueOnMismatch,
                        canonical != null ? args.AnchorEvery : 0UL,
                        loggerFactory.CreateLogger<ProductionValidationPolicy>()),
                    options: new Nethereum.CoreChain.Sync.FollowerOptions(
                        StartBlock: effectiveStart,
                        CheckpointEvery: args.CheckpointEvery,
                        AnchorEvery: canonical != null ? args.AnchorEvery : 0UL,
                        EndBlock: endBlock,
                        KeepLatestCheckpoints: args.KeepLatestCheckpoints),
                    chainConfig: chainConfig,
                    hardforkConfig: hardforkConfig,
                    txProcessor: txProcessor,
                    txVerifier: txVerifier,
                    canonical: canonical,
                    logger: loggerFactory.CreateLogger("MainnetChainNode"));
            }

            var node = BuildNode(bundle);
            Nethereum.CoreChain.Sync.FollowerRunResult syncResult;
            while (true)
            {
                syncResult = await node.RunAsync(ctsRoot.Token);
                if (syncResult.ExitReason != Nethereum.CoreChain.Sync.FollowerExitReason.SnapshotRestoreRequested)
                    break;

                if (args.DataDir == null || !(bundle is RocksDbChainStoreBundle))
                {
                    Console.Error.WriteLine("FATAL: snapshot restore requested but the bundle is not RocksDB-backed.");
                    return (int)SyncExitCode.OsError;
                }

                var cp = syncResult.SnapshotRestoreTarget!.Value;
                var snapshotDir = bundle.ResolveCheckpointSnapshotPath(cp.BlockNumber);
                Console.WriteLine($"Snapshot restore requested -> block {cp.BlockNumber:N0} ({snapshotDir})");
                try
                {
                    await node.DisposeAsync();
                    RocksDbChainStoreBundle.RestoreFromCheckpointDir(snapshotDir, args.DataDir);
                    bundle = RocksDbChainStoreBundle.Open(args.DataDir, journalOptions);
                    bundle.Metadata.RewindToCheckpointAtOrBefore(cp.BlockNumber);
                    if (bundle.JournalEnabled)
                        await bundle.Diffs.DeleteDiffsAboveBlockAsync(cp.BlockNumber).ConfigureAwait(false);
                    Console.WriteLine($"  state restored; re-running from block {cp.BlockNumber + 1:N0}.");
                    node = BuildNode(bundle);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"FATAL: snapshot restore failed: {ex.GetType().Name}: {ex.Message}");
                    return (int)SyncExitCode.OsError;
                }
            }

            swTotal.Stop();
            Console.WriteLine(new string('-', 80));
            if (args.DebugAccounts.Length > 0)
            {
                Console.WriteLine("Local state for --debug-accounts (after sync):");
                foreach (var addrRaw in args.DebugAccounts)
                {
                    var addr = addrRaw.StartsWith("0x") ? addrRaw : "0x" + addrRaw;
                    var acct = await bundle.State.GetAccountAsync(addr.ToLowerInvariant());
                    if (acct == null)
                    {
                        Console.WriteLine($"  {addr}: <not in state store>");
                    }
                    else
                    {
                        var balHex = acct.Balance.ToBigEndian().ToHex().TrimStart('0');
                        if (balHex.Length == 0) balHex = "0";
                        var nonceHex = acct.Nonce.ToBigEndian().ToHex().TrimStart('0');
                        if (nonceHex.Length == 0) nonceHex = "0";
                        Console.WriteLine($"  {addr}");
                        Console.WriteLine($"    balance    = 0x{balHex}");
                        Console.WriteLine($"    nonce      = 0x{nonceHex}");
                        Console.WriteLine($"    codeHash   = 0x{(acct.CodeHash != null ? acct.CodeHash.ToHex() : "")}");
                        Console.WriteLine($"    storageRoot= 0x{(acct.StateRoot != null ? acct.StateRoot.ToHex() : "")}");
                    }
                }
                Console.WriteLine(new string('-', 80));
            }

            Console.WriteLine("Sync result:");
            Console.WriteLine($"  ExitReason:       {syncResult.ExitReason}");
            Console.WriteLine($"  Last block:       {syncResult.LastExecutedBlock:N0}");
            Console.WriteLine($"  Blocks executed:  {syncResult.BlocksExecuted:N0}");
            Console.WriteLine($"  Root mismatches:  {syncResult.RootMismatches:N0}");
            Console.WriteLine($"  Rewind cycles:    {syncResult.RewindCyclesUsed:N0}");
            if (!string.IsNullOrEmpty(syncResult.Detail)) Console.WriteLine($"  Detail:           {syncResult.Detail}");
            Console.WriteLine($"  Wall time:        {swTotal.Elapsed.TotalSeconds:F1} s");

            await node.DisposeAsync();
            if (discv5Discovery != null) await discv5Discovery.DisposeAsync();
            if (discv5 != null) await discv5.DisposeAsync();
            return (int)SyncExitCodeMapper.FromFollowerResult(syncResult, args.ContinueOnMismatch);
        }

        /// <summary>
        /// One block worth of fetched data, ready for the consumer to replay.
        /// Body may be null if the peer failed to serve a body for the header
        /// (we still pass through so stats stay accurate).
        /// </summary>
        private sealed class BlockBundle
        {
            public BlockHeader Header { get; }
            public BlockBody Body { get; }
            public byte[] HeaderHash { get; }
            public BlockBundle(BlockHeader h, BlockBody b, byte[] hash)
            { Header = h; Body = b; HeaderHash = hash; }
        }

        /// <summary>
        /// Build the eth/68 Status template that PeerListener asserts when
        /// MirrorRemoteStatus=false (--serve-empty false). Reports our actual
        /// cursor — last_block hash + 0 TD (we don't track PoW TD; modern peers
        /// past Merge ignore it anyway) — so a peer at a different head can
        /// detect the gap. Used purely as a fallback by PeerListener when
        /// mirror is on; required identity when mirror is off.
        /// </summary>
        private static Eth68StatusMessage BuildLocalStatusTemplate(IChainStoreBundle bundle)
        {
            var lastHash = bundle.Metadata.GetLastBlockHash();
            // Fall back to mainnet genesis hash when nothing past genesis has
            // been committed (fresh load before the first batch lands).
            var bestHash = lastHash ?? MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var genesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            return new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = MainnetGenesisConstants.ChainId,
                TotalDifficulty = BigInteger.Zero,
                BestHash = bestHash,
                GenesisHash = genesisHash,
                ForkHash = 0u,
                ForkNext = 0,
            };
        }

        private static IList<Nethereum.CoreChain.WithdrawalEntry> ConvertWithdrawals(IList<Withdrawal> wlist)
        {
            if (wlist == null || wlist.Count == 0) return null;
            var result = new List<Nethereum.CoreChain.WithdrawalEntry>(wlist.Count);
            foreach (var w in wlist)
            {
                var addr = "0x" + w.Address.ToHex();
                result.Add(new Nethereum.CoreChain.WithdrawalEntry(addr, w.AmountInGwei));
            }
            return result;
        }

        private static async Task<Dictionary<string, BlockBody>> FetchBodiesAsync(
            RotatingPeerSession peer, List<BlockHeader> headers, int batchSize, CancellationToken ct)
        {
            var byHash = new Dictionary<string, BlockBody>(headers.Count);
            var hashes = headers.Select(h => Keccak(BlockHeaderEncoder.Current.Encode(h))).ToList();
            var remaining = headers.Count;
            int offset = 0;
            while (remaining > 0)
            {
                int take = Math.Min(batchSize, remaining);
                var requestHashes = hashes.GetRange(offset, take);
                var bodies = await peer.GetBodiesAsync(requestHashes, ct);

                // Some peers reply with fewer bodies than requested; map positionally
                // and refetch missing ones on the next iteration.
                int matched = Math.Min(bodies.Count, requestHashes.Count);
                for (int i = 0; i < matched; i++)
                    byHash[requestHashes[i].ToHex()] = bodies[i];

                if (matched == 0) break; // peer can't serve this slice, give up

                offset += matched;
                remaining -= matched;
            }
            return byHash;
        }

        private static byte[] Keccak(byte[] data) => new Nethereum.Util.Sha3Keccack().CalculateHash(data);

        /// <summary>
        /// On a state-root mismatch, dump the block header and tx hashes so the
        /// user can cross-reference against canonical mainnet data
        /// (Etherscan / `eth_getBlockByNumber` / `eth_getTransactionReceipt`).
        /// </summary>
        private static void DumpBlockOnMismatch(BlockHeader header, BlockBody body)
        {
            Console.WriteLine($"    Block #{(ulong)header.BlockNumber}");
            Console.WriteLine($"      coinbase     = {header.Coinbase}");
            Console.WriteLine($"      difficulty   = {header.Difficulty}");
            Console.WriteLine($"      gasLimit     = {header.GasLimit}");
            Console.WriteLine($"      gasUsed      = {header.GasUsed}");
            Console.WriteLine($"      timestamp    = {header.Timestamp}");
            Console.WriteLine($"      stateRoot    = 0x{header.StateRoot.ToHex()}");
            Console.WriteLine($"      receiptsHash = 0x{header.ReceiptHash.ToHex()}");
            Console.WriteLine($"      txCount      = {body.Transactions?.Count ?? 0}");

            if (body.Transactions != null)
            {
                for (int i = 0; i < body.Transactions.Count; i++)
                {
                    var raw = body.Transactions[i].GetRLPEncoded();
                    var txHash = Keccak(raw).ToHex();
                    Console.WriteLine($"      tx[{i}] hash = 0x{txHash}  rawLen={raw.Length}");
                    Console.WriteLine($"             raw  = 0x{raw.ToHex()}");
                }
            }
        }

        private static string Pct(long part, long whole) =>
            whole == 0 ? "  —  " : $"{(part * 100.0 / whole):F2}%";

        private static void Banner(CliArgs args)
        {
            Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Nethereum DevP2P Sync Node — live mainnet from-genesis replay validator      │");
            Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
            Console.WriteLine($"  --start-block {args.StartBlock:N0}   --blocks {args.Blocks:N0}   --report-every {args.ReportEvery}");
            if (args.Peer != null) Console.WriteLine($"  --peer {args.Peer}");
            Console.WriteLine();
        }

        private sealed class ReplayStats
        {
            public long BlocksProcessed;
            public long RootMatches;
            public long RootMismatches;
            public long BodiesMissing;
            public long TransactionsExecuted;
        }

        private static long DirectorySize(string path)
        {
            if (!System.IO.Directory.Exists(path)) return 0;
            long total = 0;
            try
            {
                foreach (var f in System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories))
                {
                    try { total += new System.IO.FileInfo(f).Length; }
                    catch { /* file may have vanished mid-walk */ }
                }
            }
            catch { /* permission / race — best-effort */ }
            return total;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double v = bytes;
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < suffix.Length - 1) { v /= 1024; i++; }
            return $"{v:F1} {suffix[i]}";
        }
    }
}
