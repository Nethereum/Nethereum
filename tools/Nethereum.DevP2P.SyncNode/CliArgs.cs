using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>Minimal hand-rolled CLI args parser — no external dependency.</summary>
    internal sealed class CliArgs
    {
        public ulong StartBlock { get; private set; } = 1;
        public ulong Blocks { get; private set; } = 1000;
        public string Peer { get; private set; } = null;       // null → try MainnetBootnodes in turn
        public int ReportEvery { get; private set; } = 50;
        public bool ContinueOnMismatch { get; private set; } = false;
        public bool ShowHelp { get; private set; } = false;
        public int HeadersBatch { get; private set; } = 192;   // Geth's MaxHeaderFetch
        public int BodiesBatch { get; private set; } = 64;     // safe-ish batch for older Frontier blocks
        public string DataDir { get; private set; } = null;    // null → in-memory only (volatile)
        public int FlushEvery { get; private set; } = 200;     // commit RocksDB + last-block metadata every N blocks
        public int PrefetchDepth { get; private set; } = 2000; // pipelined sync: bounded queue of fetched-but-not-executed blocks. Default sized so the executor never starves waiting for the fetcher across normal RTT + body-batch jitter.
        public ulong CheckpointEvery { get; private set; } = 50_000; // periodic audit + state snapshot every N blocks; 0 = off
        public int? KeepLatestCheckpoints { get; private set; } = 5; // cap on retained checkpoints; older auto-deleted after each new SaveCheckpointAsync. Mainnet at 7 GB/cp keeps ~35 GB of snapshot overhead. Set 0 / negative via CLI to disable (unbounded) but be warned: 29 cps once consumed 90 GB and crashed sync.
        public ulong AnchorEvery { get; private set; } = 25_000;     // canonical cross-check cadence; 0 = off
        public string PeerCachePath { get; private set; } = "peer-cache.json"; // default in CWD; null = off
        public string[] DebugAccounts { get; private set; } = System.Array.Empty<string>();
        public ulong? RewindToCheckpoint { get; private set; } = null; // when set: rewind --data-dir to nearest checkpoint ≤ N, then exit
        public ulong? SnapshotCheckpoint { get; private set; } = null; // when set: hard-link snapshot of --data-dir to SnapshotOutput, rewound to nearest checkpoint ≤ N
        public string SnapshotOutput { get; private set; } = null;
        public HashSet<ulong> DumpFixtureBlocks { get; private set; } = new(); // when block N is processed and is in this set, emit fixture JSON
        public string DumpFixtureOutputDir { get; private set; } = ResolveDefaultFixtureDir();

        private static string ResolveDefaultFixtureDir()
        {
            // Walk up from the binary location looking for the EVM.UnitTests fixtures dir.
            // Found (dev mode) → default-on so every state-root mismatch auto-emits a regression cell.
            // Not found (shipped binary outside repo) → null, opt-in via --dump-fixture-output.
            try
            {
                var dir = System.IO.Path.GetDirectoryName(typeof(CliArgs).Assembly.Location);
                for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
                {
                    var candidate = System.IO.Path.Combine(dir, "tests", "Nethereum.EVM.UnitTests", "Fixtures", "MainnetBlocks");
                    if (System.IO.Directory.Exists(candidate)) return candidate;
                    dir = System.IO.Path.GetDirectoryName(dir);
                }
            }
            catch { /* fall through to null */ }
            return null;
        }
        // Inbound RLPx TCP listener.
        //   N > 0 : bind that port (default 30303). Listener accepts inbound peers
        //           and serves eth/68 from our backend stores.
        //   N = 0 : bind an OS-chosen ephemeral TCP port (still enabled).
        //   N < 0 : disable inbound serving entirely (client-only).
        public int ListenPort { get; private set; } = 30303;
        // Serve-empty toggle (a common default behaviour). When true the inbound
        // handshake mirrors the remote peer's chain identifiers so we accept
        // inbound peers regardless of our own sync state — essential during
        // from-genesis bootstrap. When false the listener asserts our own
        // (genesis-only, no head data) chain identifiers and a peer at a
        // different head will reject our Status reply.
        public bool ServeEmpty { get; private set; } = true;
        public int Discv5Port { get; private set; } = 0; // 0 = bind an OS-chosen ephemeral UDP port (still enabled). Set to a specific port to bind there. Ignored when DisableDiscv5 is true.
        public bool DisableDiscv5 { get; private set; } = false; // discv5 is the third peer-discovery source (alongside discv4 bootnodes + EIP-1459 DNS). On by default for mainnet. Set --disable-discv5 to opt out.
        public int TargetPeers { get; private set; } = 16; // PeerPoolManager keeps dialling candidates until ActivePeers >= this, then idles. Higher = more parallelism on body fetch; lower = less network churn.
        // Re-execute mode: read headers + txs + uncles from local --data-dir and replay
        // each block through BlockProcessor without dialling any peers. Lets you
        // validate an EVM fix in minutes instead of hours of re-fetch.
        public ulong? ReExecuteFrom { get; private set; } = null;
        public ulong? ReExecuteTo { get; private set; } = null;
        // Wipe state trie + state-accounts + receipts + logs + audit checkpoints,
        // KEEP headers + txs + uncles + metadata, then exit. Pair with
        // --re-execute-from 1 in the next run to rebuild state on the fixed EVM.
        public bool ResetState { get; private set; } = false;
        // Selective checkpoint pruning. Keep checkpoints whose block number is a
        // multiple of N, drop all others. Frees significant disk — once full chain
        // data is in the live DB, any state can be rebuilt via --re-execute-from N,
        // so fine-grained checkpoints are pure speed cache. Use 1_000_000 to keep
        // every 1M blocks as anchors. Exits after pruning.
        public ulong? PruneCheckpointsKeepEvery { get; private set; } = null;
        // Reverse-diff journal retention (HistoricalStateStore + IStateDiffStore).
        // Enables short-window rewind without re-execution and historical state
        // queries (eth_call at past block, debug_traceTransaction within retention).
        // Set 0 to disable the journal entirely (smaller DB, no historical reads).
        public int JournalBlocks { get; private set; } = 1024;
        // Trusted JSON-RPC endpoint consulted on state-root divergence to
        // diagnose EVM bug vs bad-peer header. When set, auto-rewind
        // ConsensusSource.DiagnoseAsync against this RPC before rewinding.
        // null/empty → skip diagnosis, fall through to existing rewind +
        // ban-peer policy. Example: --canonical-rpc https://mainnet.infura.io/v3/<KEY>
        public string CanonicalRpc { get; private set; } = null;

        public static CliArgs Parse(string[] argv)
        {
            var args = new CliArgs();
            for (int i = 0; i < argv.Length; i++)
            {
                var a = argv[i];
                switch (a)
                {
                    case "--help":
                    case "-h":
                        args.ShowHelp = true; break;
                    case "--start-block":
                        args.StartBlock = ulong.Parse(RequireValue(argv, ref i)); break;
                    case "--blocks":
                        args.Blocks = ulong.Parse(RequireValue(argv, ref i)); break;
                    case "--peer":
                        args.Peer = RequireValue(argv, ref i); break;
                    case "--report-every":
                        args.ReportEvery = int.Parse(RequireValue(argv, ref i)); break;
                    case "--headers-batch":
                        args.HeadersBatch = int.Parse(RequireValue(argv, ref i)); break;
                    case "--bodies-batch":
                        args.BodiesBatch = int.Parse(RequireValue(argv, ref i)); break;
                    case "--continue-on-mismatch":
                        args.ContinueOnMismatch = true; break;
                    case "--data-dir":
                        args.DataDir = RequireValue(argv, ref i); break;
                    case "--flush-every":
                        args.FlushEvery = int.Parse(RequireValue(argv, ref i)); break;
                    case "--checkpoint-every":
                        args.CheckpointEvery = ulong.Parse(RequireValue(argv, ref i)); break;
                    case "--keep-checkpoints":
                    {
                        int v = int.Parse(RequireValue(argv, ref i));
                        args.KeepLatestCheckpoints = v > 0 ? (int?)v : null;
                        break;
                    }
                    case "--anchor-every":
                        args.AnchorEvery = ulong.Parse(RequireValue(argv, ref i)); break;
                    case "--prefetch-depth":
                        args.PrefetchDepth = int.Parse(RequireValue(argv, ref i)); break;
                    case "--peer-cache":
                        args.PeerCachePath = RequireValue(argv, ref i);
                        if (args.PeerCachePath.Equals("off", System.StringComparison.OrdinalIgnoreCase) || args.PeerCachePath == "-")
                            args.PeerCachePath = null;
                        break;
                    case "--listen-port":
                        args.ListenPort = int.Parse(RequireValue(argv, ref i));
                        break;
                    case "--serve-empty":
                        args.ServeEmpty = ParseBool(RequireValue(argv, ref i), "--serve-empty");
                        break;
                    case "--no-serve-empty":
                        args.ServeEmpty = false;
                        break;
                    case "--discv5-port":
                        args.Discv5Port = int.Parse(RequireValue(argv, ref i));
                        break;
                    case "--disable-discv5":
                        args.DisableDiscv5 = true;
                        break;
                    case "--target-peers":
                        args.TargetPeers = int.Parse(RequireValue(argv, ref i));
                        break;
                    case "--debug-accounts":
                        args.DebugAccounts = RequireValue(argv, ref i).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                        break;
                    case "--rewind-to-checkpoint":
                        args.RewindToCheckpoint = ulong.Parse(RequireValue(argv, ref i));
                        break;
                    case "--snapshot-checkpoint":
                        args.SnapshotCheckpoint = ulong.Parse(RequireValue(argv, ref i));
                        break;
                    case "--snapshot-output":
                        args.SnapshotOutput = RequireValue(argv, ref i);
                        break;
                    case "--dump-fixture-blocks":
                        foreach (var n in RequireValue(argv, ref i).Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                            args.DumpFixtureBlocks.Add(ulong.Parse(n.Trim()));
                        break;
                    case "--dump-fixture-output":
                        args.DumpFixtureOutputDir = RequireValue(argv, ref i);
                        break;
                    case "--re-execute-from":
                        args.ReExecuteFrom = ulong.Parse(RequireValue(argv, ref i));
                        break;
                    case "--re-execute-to":
                        args.ReExecuteTo = ulong.Parse(RequireValue(argv, ref i));
                        break;
                    case "--reset-state":
                        args.ResetState = true;
                        break;
                    case "--prune-checkpoints":
                        args.PruneCheckpointsKeepEvery = ulong.Parse(RequireValue(argv, ref i));
                        break;
                    case "--journal-blocks":
                        args.JournalBlocks = int.Parse(RequireValue(argv, ref i));
                        if (args.JournalBlocks < 0) args.JournalBlocks = 0;
                        break;
                    case "--canonical-rpc":
                        args.CanonicalRpc = RequireValue(argv, ref i);
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {a}. Use --help for usage.");
                }
            }
            return args;
        }

        private static string RequireValue(string[] argv, ref int i)
        {
            if (i + 1 >= argv.Length) throw new ArgumentException($"Missing value after {argv[i]}");
            return argv[++i];
        }

        private static bool ParseBool(string value, string flag)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing value after {flag}");
            switch (value.Trim().ToLowerInvariant())
            {
                case "true": case "1": case "on": case "yes":  return true;
                case "false": case "0": case "off": case "no": return false;
                default:
                    throw new ArgumentException(
                        $"{flag} expects true|false|on|off|yes|no|1|0, got '{value}'");
            }
        }

        /// <summary>
        /// Translate <see cref="JournalBlocks"/> into a
        /// <see cref="HistoricalStateOptions"/> instance for the storage
        /// backend. Returns null when the journal is disabled
        /// (<c>--journal-blocks 0</c>).
        /// </summary>
        public HistoricalStateOptions BuildJournalOptions()
        {
            if (JournalBlocks <= 0) return null;
            return new HistoricalStateOptions
            {
                MaxHistoryBlocks = JournalBlocks,
                EnablePruning = true,
                // Prune in ~16% of the retention window so the journal CFs
                // don't run far past the configured size between sweeps. At
                // the default 1024-block retention that's every ~64 blocks.
                PruningIntervalBlocks = Math.Max(64, JournalBlocks / 16),
            };
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Nethereum DevP2P Sync Node — live mainnet from-genesis replay validator");
            Console.WriteLine();
            Console.WriteLine("Usage: nethereum-syncnode [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --blocks N                Number of blocks to fetch and replay (default 1000)");
            Console.WriteLine("  --start-block N           First block to process (default 1, must be > 0)");
            Console.WriteLine("  --peer enode://…          Specific peer to dial (default: try Mainnet bootnodes in turn)");
            Console.WriteLine("  --report-every N          Print a progress line every N blocks (default 50)");
            Console.WriteLine("  --headers-batch N         Header request batch size (default 192)");
            Console.WriteLine("  --bodies-batch N          Body request batch size (default 64)");
            Console.WriteLine("  --continue-on-mismatch    Keep going past a state-root mismatch (default: stop)");
            Console.WriteLine("  --data-dir PATH           RocksDB chain data dir. Resumes from last_block on restart.");
            Console.WriteLine("                            Default: in-memory only (state lost on exit).");
            Console.WriteLine("  --flush-every N           Flush RocksDB + persist last_block every N blocks (default 200)");
            Console.WriteLine("  --checkpoint-every N      Save an audit checkpoint (block N, state root, block hash) every N blocks");
            Console.WriteLine("                            (default 50,000). Limits worst-case rewind cost on a peer-consensus");
            Console.WriteLine("                            divergence — at ~50 b/s that's ≈17 min of re-execution per cycle.");
            Console.WriteLine("                            Set 0 to disable periodic checkpoints (rewind only via journal).");
            Console.WriteLine("  --keep-checkpoints N      Auto-prune so only the N most recent checkpoints stay on disk (default 5).");
            Console.WriteLine("                            Pass 0 (or any value ≤0) to disable auto-prune. Each mainnet checkpoint");
            Console.WriteLine("                            is several GB and unbounded accumulation exhausts disk: 29 cps once");
            Console.WriteLine("                            consumed 90 GB and crashed sync at block 1.46M.");
            Console.WriteLine("  --anchor-every N          Cross-check our committed state root against --canonical-rpc every N");
            Console.WriteLine("                            blocks (default 25,000). On mismatch, routes through OnVerdict and the");
            Console.WriteLine("                            same validating-rewind path as a peer-driven divergence. Requires");
            Console.WriteLine("                            --canonical-rpc. Set 0 to disable periodic anchoring.");
            Console.WriteLine("  --prefetch-depth N        Pipeline buffer size (default 2000). Producer task fetches up to N");
            Console.WriteLine("                            blocks ahead of execution. Sized so the executor never starves while");
            Console.WriteLine("                            waiting for the fetcher across normal RTT + body-batch jitter — at");
            Console.WriteLine("                            small batch sizes (Frontier) the executor used to stall under 200.");
            Console.WriteLine("  --peer-cache PATH         Persistent peer cache file (default 'peer-cache.json' in cwd).");
            Console.WriteLine("                            Records every successfully-bonded peer; dialled first on restart.");
            Console.WriteLine("                            Pass 'off' or '-' to disable.");
            Console.WriteLine("  --rewind-to-checkpoint N  Rewind --data-dir to the nearest checkpoint at or below block N,");
            Console.WriteLine("                            then exit (no sync). Subsequent runs resume from that checkpoint+1.");
            Console.WriteLine("                            Requires --data-dir. Destructive — overwrites metadata.last_block.");
            Console.WriteLine("                            Use for fix-validate cycles after a known divergence.");
            Console.WriteLine("  --snapshot-checkpoint N   Non-destructive: hard-link a new RocksDB at --snapshot-output,");
            Console.WriteLine("                            rewound to the nearest checkpoint at or below block N, then exit.");
            Console.WriteLine("                            Live --data-dir is untouched. Use for audit/forensics: open the");
            Console.WriteLine("                            snapshot with --data-dir <output> and replay forward N+1, N+2 etc.");
            Console.WriteLine("  --snapshot-output PATH    Output path for --snapshot-checkpoint (must NOT exist).");
            Console.WriteLine("  --debug-accounts a,b,c    Dump account state for these addresses on state-root mismatch.");
            Console.WriteLine("  --reset-state             Wipe state trie + receipts + logs + checkpoints, KEEP headers");
            Console.WriteLine("                            + txs + uncles + metadata, then exit. Pair with --re-execute-from 1");
            Console.WriteLine("                            on the next run to rebuild state on a fixed EVM without re-fetching.");
            Console.WriteLine("  --re-execute-from N       Read locally-stored headers + txs + uncles from block N forward and");
            Console.WriteLine("                            replay through BlockProcessor — no network traffic. Validates state");
            Console.WriteLine("                            root against each stored header. Use after --reset-state to verify");
            Console.WriteLine("                            an EVM fix end-to-end against the chain data already on disk.");
            Console.WriteLine("  --re-execute-to M         Stop --re-execute-from at block M (default: last persisted header).");
            Console.WriteLine("  --prune-checkpoints N     Keep checkpoints whose block number is a multiple of N, drop others.");
            Console.WriteLine("                            E.g. --prune-checkpoints 1000000 keeps every 1M-block anchor.");
            Console.WriteLine("                            Frees disk; chain data in live DB still lets --re-execute-from rebuild");
            Console.WriteLine("                            any state. Exits after pruning. Requires --data-dir.");
            Console.WriteLine("  --journal-blocks N        Reverse-diff journal retention window (default 1024). Records the");
            Console.WriteLine("                            pre-value of every account/storage write so short-window rewinds (reorgs,");
            Console.WriteLine("                            peer-consensus divergences) can replay backward without re-execution.");
            Console.WriteLine("                            Also unlocks historical state queries (eth_call at past block,");
            Console.WriteLine("                            debug_traceTransaction within retention). Set 0 to disable.");
            Console.WriteLine("  --canonical-rpc URL       Trusted JSON-RPC endpoint consulted on state-root divergence to");
            Console.WriteLine("                            diagnose EVM bug vs bad-peer header. Auto-rewind calls");
            Console.WriteLine("                            eth_getBlockByNumber on the RPC and compares against the peer.");
            Console.WriteLine("                            Halts on EVM bug (unless --continue-on-mismatch), bans peer otherwise.");
            Console.WriteLine("                            Example: --canonical-rpc https://mainnet.infura.io/v3/<key>");
            Console.WriteLine("  --listen-port N           Bind RlpxListener for inbound peers. Default 30303.");
            Console.WriteLine("                            N > 0 binds that TCP port. N = 0 binds an OS-chosen ephemeral port.");
            Console.WriteLine("                            N < 0 disables inbound serving entirely (client-only).");
            Console.WriteLine("                            Inbound peers go through NetRestrict + per-IP throttle + MaxPeers +");
            Console.WriteLine("                            per-subnet caps + trusted-peers bypass (same gates as outbound).");
            Console.WriteLine("                            Bound TCP port is advertised in our discv5 ENR.");
            Console.WriteLine("  --serve-empty BOOL        Accept inbound peers even before we're synced (default true).");
            Console.WriteLine("                            true = listener mirrors remote peer's chain identifiers in our");
            Console.WriteLine("                            Status reply, so any peer at any chain head is admitted; they'll");
            Console.WriteLine("                            still see empty responses for blocks above our cursor (see #219).");
            Console.WriteLine("                            false = listener asserts our own chain identifiers; peers ahead");
            Console.WriteLine("                            of us reject our Status. Disable only for restricted deployments.");
            Console.WriteLine("                            Synonym: --no-serve-empty.");
            Console.WriteLine("  --discv5-port N           Bind the discv5 UDP listener to port N. Default: 0 (OS-chosen");
            Console.WriteLine("                            ephemeral port). discv5 is enabled by default and runs the");
            Console.WriteLine("                            mainnet bootnode bond + FINDNODE walk; discovered ENRs are");
            Console.WriteLine("                            piped into the same peer-pool candidate stream as discv4");
            Console.WriteLine("                            bootnodes + EIP-1459 DNS. Ignored when --disable-discv5 is set.");
            Console.WriteLine("  --disable-discv5          Disable the discv5 peer-discovery source (default: enabled).");
            Console.WriteLine("                            Leaves discv4 bootnodes + EIP-1459 DNS + persistent peer cache");
            Console.WriteLine("                            as the only peer sources. Useful for AppChain VPC deployments");
            Console.WriteLine("                            where outbound UDP to mainnet discovery is restricted.");
            Console.WriteLine("  --target-peers N          Steady-state peer pool size (default 16). The dialer fires until");
            Console.WriteLine("                            ActivePeers reaches this, then idles. Higher = more parallel");
            Console.WriteLine("                            body-fetch throughput; lower = less network churn. Tune up if");
            Console.WriteLine("                            sync is body-fetch bound and remote peers churn fast.");
            Console.WriteLine("  --help, -h                Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  nethereum-syncnode --blocks 100");
            Console.WriteLine("  nethereum-syncnode --data-dir ./chaindata/mainnet --blocks 100000");
            Console.WriteLine("  nethereum-syncnode --data-dir ./chaindata/mainnet     # resume from last_block");
            Console.WriteLine("  nethereum-syncnode --peer enode://abc...@1.2.3.4:30303 --blocks 200");
        }
    }
}
