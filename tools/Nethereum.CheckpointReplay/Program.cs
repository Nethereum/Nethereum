using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CheckpointReplay
{
    // Controlled OFFLINE replay of mainnet blocks through the host BlockExecutor + IncrementalStateRootCalculator
    // against a CERTIFIED-CLEAN path-keyed checkpoint (opened READ-ONLY), driven deterministically (no follower).
    // Block bodies/headers/withdrawals come from a geth archive-of-bodies RPC; historical STATE comes ONLY from
    // the checkpoint (pre-state for the first block) accumulated forward through in-memory overlays.
    internal static class Program
    {
        public static async Task<int> Main(string[] argv)
        {
            string checkpoint = null, rpc = null, dumpReads = null, getNode = null, prevRootHex = null;
            string rootHex = null, readsFile = null, historyOwner = null; bool verifyDescent = false;
            long from = 25548476, to = 25548480;
            bool freshPerBlock = false;
            for (int i = 0; i < argv.Length; i++)
            {
                switch (argv[i])
                {
                    case "--checkpoint": checkpoint = argv[++i]; break;
                    case "--rpc": rpc = argv[++i]; break;
                    case "--from": from = long.Parse(argv[++i]); break;
                    case "--to": to = long.Parse(argv[++i]); break;
                    case "--fresh-per-block": freshPerBlock = true; break;
                    case "--dump-reads": dumpReads = argv[++i]; break;
                    case "--get-node": getNode = argv[++i]; break; // "<ownerHex>:<pathHex>" raw node probe (no exec)
                    case "--prev-root": prevRootHex = argv[++i].Replace("0x", ""); break; // force warm-start root
                    case "--verify-descent": verifyDescent = true; break;
                    case "--root": rootHex = argv[++i].Replace("0x", ""); break;
                    case "--reads": readsFile = argv[++i]; break;
                    case "--history": historyOwner = argv[++i].Replace("0x", ""); break;
                }
            }

            // History probe: print every block at which a node under this storage owner was written (root path
            // flagged). Answers whether the root was last written during the heal (<=475) or forward exec (476+).
            if (historyOwner != null && checkpoint != null)
            {
                Console.WriteLine($"[open ] read-only {checkpoint}");
                using var r = new CheckpointReader(checkpoint);
                Console.WriteLine("[open ] OK");
                r.DumpHistory(historyOwner.HexToByteArray());
                return 0;
            }

            // Raw node probe: open <checkpoint> READ-ONLY and print the trie-node blob + keccak at (owner,path).
            // Used to 3-way compare the corrupt (owner,path) across LIVE store vs certified checkpoint (no RPC/exec).
            if (getNode != null && checkpoint != null)
            {
                var parts = getNode.Split(':');
                var ownerHex = parts[0];
                var pathHex = parts.Length > 1 ? parts[1] : "";
                var owner = ownerHex.Length == 0 ? Array.Empty<byte>() : ownerHex.HexToByteArray();
                var pathBytes = pathHex.Length == 0 ? Array.Empty<byte>() : pathHex.HexToByteArray();
                Console.WriteLine($"[open ] read-only {checkpoint}");
                using var r = new CheckpointReader(checkpoint);
                Console.WriteLine("[open ] OK");
                var blob = r.GetTrieNode(owner, pathBytes);
                if (blob == null) { Console.WriteLine($"NODE owner={ownerHex} path={pathHex} => ABSENT (null)"); return 0; }
                var kh = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider().ComputeHash(blob);
                Console.WriteLine($"NODE owner={ownerHex} path={pathHex} len={blob.Length} keccak={kh.ToHex()} blob={blob.ToHex()}");
                return 0;
            }

            // FORENSIC descent-verify (no RPC / no EVM). Descend the account trie from --root in the store at
            // --checkpoint (the LIVE store), bounded to the (owner,path) node set block 480 reads (--reads =
            // block480_cold_reads.txt). Every node is loaded through OverlayNodeStore.Get which verifies
            // keccak(storedBlob) == parent-referenced hash and throws the named CORRUPT-NODE exception at the
            // FIRST node whose live blob does not match the hash its parent references. Then descend each
            // touched contract's storage trie from that account's live StateRoot the same way.
            if (verifyDescent && checkpoint != null && rootHex != null && readsFile != null)
            {
                var hp = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
                var decoder = new Nethereum.Merkle.Patricia.Nodes.Rlp.NodeRlpDecoder(hp);
                Console.WriteLine($"[open ] read-only {checkpoint}");
                using var rr = new CheckpointReader(checkpoint);
                Console.WriteLine("[open ] OK");
                var ov = new OverlayNodeStore(rr);

                // Parse cold-reads: group target node paths (hex) by owner-hex ("" = account trie).
                var targetsByOwner = new Dictionary<string, List<string>>();
                foreach (var raw in System.IO.File.ReadAllLines(readsFile))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("OWNER")) continue;
                    var slash = line.IndexOf('/');
                    if (slash < 0) continue;
                    var o = line.Substring(0, slash).Trim();
                    var p = line.Substring(slash + 1).Trim();
                    if (!targetsByOwner.TryGetValue(o, out var lst)) { lst = new List<string>(); targetsByOwner[o] = lst; }
                    lst.Add(p);
                }
                var accountTargets = targetsByOwner.TryGetValue("", out var at) ? at : new List<string>();
                // Also descend to each touched contract's ACCOUNT leaf (its full key = keccak(addr) = the storage
                // owner). The cold-read set lists only internal account nodes, so add the full-key nibble paths to
                // reach the leaves and capture their live storageRoot for the storage-trie descent.
                foreach (var o in targetsByOwner.Keys)
                    if (o.Length > 0) accountTargets.Add(o.HexToByteArray().ConvertToNibbles().ToHex());
                Console.WriteLine($"[reads] account-trie target nodes={accountTargets.Count} (incl. {targetsByOwner.Count - (targetsByOwner.ContainsKey("") ? 1 : 0)} contract full-keys); storage owners={targetsByOwner.Count - (targetsByOwner.ContainsKey("") ? 1 : 0)}");

                bool OnPath(byte[] path, List<string> targets)
                {
                    var ph = path == null ? "" : path.ToHex();
                    foreach (var t in targets) if (t.StartsWith(ph)) return true;
                    return false;
                }

                // ownerHex(keccak addr) -> storageRoot captured from account leaves during the account descent.
                var storageRootByOwner = new Dictionary<string, byte[]>();
                // ownerHex -> trie leaf raw account RLP for flat-vs-trie diff.
                var trieLeafValueByOwner = new Dictionary<string, byte[]>();

                void Walk(Nethereum.Merkle.Patricia.Nodes.Node node, byte[] nodePath, byte[] owner, List<string> targets, bool account)
                {
                    switch (node)
                    {
                        case Nethereum.Merkle.Patricia.Nodes.LeafNode leaf:
                            if (account)
                            {
                                var full = nodePath.ConcatArrays(leaf.Nibbles);
                                if (full.Length == 64)
                                {
                                    var ownerKey = full.ConvertFromNibbles().ToHex();
                                    var acct = Nethereum.RLP.RLP.Decode(leaf.Value) as Nethereum.RLP.RLPCollection;
                                    if (acct != null && acct.Count >= 4)
                                    {
                                        storageRootByOwner[ownerKey] = acct[2].RLPData;
                                        trieLeafValueByOwner[ownerKey] = leaf.Value;
                                    }
                                }
                            }
                            return;
                        case Nethereum.Merkle.Patricia.Nodes.ExtendedNode ext:
                            WalkChild(ext.InnerNode, nodePath.ConcatArrays(ext.Nibbles), owner, targets, account); return;
                        case Nethereum.Merkle.Patricia.Nodes.BranchNode br:
                            for (int i = 0; i < br.Children.Length; i++)
                                WalkChild(br.Children[i], nodePath.ConcatArrays(new byte[] { (byte)i }), owner, targets, account);
                            return;
                    }
                }
                void WalkChild(Nethereum.Merkle.Patricia.Nodes.Node child, byte[] childPath, byte[] owner, List<string> targets, bool account)
                {
                    if (child == null || child is Nethereum.Merkle.Patricia.Nodes.EmptyNode) return;
                    if (child is Nethereum.Merkle.Patricia.Nodes.HashNode hn)
                    {
                        if (!OnPath(childPath, targets)) return;
                        var decoded = decoder.Decode(hn, ov, false); // STORE READ + verify-on-read (throws on mismatch)
                        Walk(decoded, childPath, owner, targets, account);
                    }
                    else Walk(child, childPath, owner, targets, account); // embedded node, materialised inside parent blob
                }

                // ---- Account trie descent ----
                var accRoot = new Nethereum.Merkle.Patricia.Nodes.HashNode(hp)
                { Hash = rootHex.HexToByteArray(), Owner = Array.Empty<byte>(), Path = Array.Empty<byte>() };
                try
                {
                    var decodedRoot = decoder.Decode(accRoot, ov, false);
                    Walk(decodedRoot, Array.Empty<byte>(), Array.Empty<byte>(), accountTargets, true);
                    Console.WriteLine($"[account] descends CLEAN over {accountTargets.Count} target nodes from root 0x{rootHex}. leaves captured={storageRootByOwner.Count}");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine("[account] *** NODE MISMATCH DURING ACCOUNT-TRIE DESCENT (from root 0x" + rootHex + ") ***");
                    Console.WriteLine(ex.Message);
                    return 20;
                }

                // FLAT vs TRIE: for every captured trie leaf, compare the trie account (as of --root) to the LIVE
                // flat account row (CF_STATE_ACCOUNTS[keccak(addr)]). A divergence = flat/node desync corruption.
                int flatChecked = 0, flatMismatch = 0;
                foreach (var kv in trieLeafValueByOwner)
                {
                    var flat = rr.GetAccountByHash(kv.Key.HexToByteArray());
                    flatChecked++;
                    var flatEnc = flat == null ? null : Nethereum.Model.AccountEncoder.Current.Encode(flat);
                    bool same = flatEnc != null && ByteUtil.AreEqual(flatEnc, kv.Value);
                    if (!same && flatMismatch < 15)
                    {
                        Console.WriteLine($"[flat!=trie] owner(keccakAddr)={kv.Key}");
                        Console.WriteLine($"    trieLeafRLP=0x{kv.Value.ToHex()}");
                        Console.WriteLine($"    flatEncRLP ={(flatEnc == null ? "(flat row ABSENT)" : "0x" + flatEnc.ToHex())}");
                    }
                    if (!same) flatMismatch++;
                }
                Console.WriteLine($"[flat-vs-trie] checked={flatChecked} mismatches={flatMismatch}");

                // ---- Storage trie descent per touched contract ----
                var emptyTrie = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421";
                foreach (var kv in targetsByOwner)
                {
                    if (kv.Key.Length == 0) continue;
                    var ownerHex = kv.Key;
                    if (!storageRootByOwner.TryGetValue(ownerHex, out var sroot))
                    { Console.WriteLine($"[storage] owner={ownerHex} SKIP (account leaf/storageRoot not reached in bounded descent)"); continue; }
                    if (sroot == null || sroot.ToHex() == emptyTrie || sroot.Length != 32)
                    { continue; }
                    var sRootRef = new Nethereum.Merkle.Patricia.Nodes.HashNode(hp)
                    { Hash = sroot, Owner = ownerHex.HexToByteArray(), Path = Array.Empty<byte>() };
                    try
                    {
                        var decodedS = decoder.Decode(sRootRef, ov, false);
                        Walk(decodedS, Array.Empty<byte>(), ownerHex.HexToByteArray(), kv.Value, false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.WriteLine($"[storage] *** CORRUPT NODE FOUND IN STORAGE TRIE owner={ownerHex} (storageRoot=0x{sroot.ToHex()}) ***");
                        Console.WriteLine(ex.Message);
                        return 21;
                    }
                }
                Console.WriteLine("[result] descent-verify complete: NO corrupt node found among block-480 cold-read set (account + storage).");
                return 0;
            }
            if (checkpoint == null || rpc == null)
            {
                Console.Error.WriteLine("usage: --checkpoint <dir> --rpc <geth-url> [--from N] [--to M]");
                return 1;
            }

            using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => { o.SingleLine = true; }).SetMinimumLevel(LogLevel.Error));
            var execLogger = loggerFactory.CreateLogger<BlockExecutor>();

            Console.WriteLine($"[open ] read-only checkpoint {checkpoint}");
            using var reader = new CheckpointReader(checkpoint);
            Console.WriteLine("[open ] OK");

            var state = new OverlayStateStore(reader);
            var nodes = new OverlayNodeStore(reader);

            Nethereum.JsonRpc.Client.ClientBase.ConnectionTimeout = TimeSpan.FromSeconds(180);
            var rpcClient = new Nethereum.JsonRpc.Client.RpcClient(new Uri(rpc));
            var web3 = new Web3.Web3(rpcClient);
            var eth = web3.Eth;
            var chainId = (await Retry(() => eth.ChainId.SendRequestAsync())).Value;
            Console.WriteLine($"[chain] chainId={chainId}");

            var blockStore = new RpcBlockStore(eth);

            // Faithful mainnet-follower wiring (MainnetChainNodeFactory). A fresh calculator + executor per block
            // (freshPerBlock) mimics the live follower's COLD descent: every node the state-root recompute needs
            // is lazy-loaded from the store (overlay+checkpoint), never carried warm in memory across blocks.
            IncrementalStateRootCalculator sharedCalc = null;
            BlockExecutor BuildEngine()
            {
                var calc = new IncrementalStateRootCalculator(state, nodes, emitTombstones: true);
                sharedCalc = calc;
                return new BlockExecutor(
                    state, blockStore, MainnetChainActivations.Instance,
                    chainConfigFactory: f => new ChainConfig
                    {
                        ChainId = chainId,
                        BaseFee = BigInteger.Zero,
                        Coinbase = AddressUtil.ZERO_ADDRESS,
                        Hardfork = f.ToString().ToLowerInvariant()
                    },
                    hardforkConfigFactory: f => KzgAwareMainnetHardforkRegistry.Instance.Get(f),
                    stateRootCalculator: calc,
                    rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                    trieNodeStore: nodes,
                    logger: execLogger);
            }
            var sharedEngine = freshPerBlock ? null : BuildEngine();

            // FORENSIC: pre-warm the shared calculator from an EXPLICIT previous state root so the block-480
            // descent starts at the CANONICAL parent (479) root rather than whatever root the executor would
            // otherwise source. This makes the cold descent read the LIVE store's nodes from the correct 479
            // root and throw verify-on-read at the FIRST node whose live blob != parent-referenced hash.
            if (prevRootHex != null && sharedCalc != null)
            {
                var prevRoot = prevRootHex.HexToByteArray();
                Console.WriteLine($"[warm ] pre-warming calculator from prev-root 0x{prevRoot.ToHex()}");
                var warmed = await sharedCalc.ComputeStateRootWithoutPersistAsync(prevRoot);
                Console.WriteLine($"[warm ] calculator root after pre-warm = 0x{warmed.ToHex()} (matches prev-root: {ByteUtil.AreEqual(warmed, prevRoot)})");
            }

            Console.WriteLine($"[mode ] {(freshPerBlock ? "FRESH calculator per block (cold descent, live-follower-faithful)" : "single warm calculator across blocks")}");
            Console.WriteLine();
            Console.WriteLine($"  {"block",12}  {"fork",-10}  {"txs",4}  {"wd",3}  match  computedStateRoot / expected");
            Console.WriteLine("  " + new string('-', 100));

            bool anyDiverged = false;
            for (long n = from; n <= to; n++)
            {
                var rpcBlock = await Retry(() => eth.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(new BlockParameter(new HexBigInteger(n))));
                if (rpcBlock == null) { Console.WriteLine($"  {n,12:N0}  block not returned"); return 2; }

                var header = BlockHeaderEncoder.Current.Decode(
                    (await new DebugGetRawHeader(eth.Client)
                        .SendRequestAsync(new BlockParameter(new HexBigInteger(n)))
                        .ConfigureAwait(false)).HexToByteArray());

                // Build txs from raw signed RLP (rpcTx.Input is calldata, not the signed envelope).
                var txEntries = new List<TxEntry>(rpcBlock.Transactions.Length);
                var blockNumHex = "0x" + n.ToString("x");
                for (int i = 0; i < rpcBlock.Transactions.Length; i++)
                {
                    var idxHex = "0x" + i.ToString("x");
                    var raw = await Retry(() => web3.Client.SendRequestAsync<string>(new RpcRequest(0,
                        "eth_getRawTransactionByBlockNumberAndIndex", blockNumHex, idxHex)));
                    var signed = TransactionFactory.CreateTransaction(raw.HexToByteArray());
                    txEntries.Add(new TxEntry(signed, rpcBlock.Transactions[i].From));
                }

                IList<WithdrawalEntry> withdrawals = null;
                var recipients = new List<string>();
                BigInteger totalGwei = 0;
                if (rpcBlock.Withdrawals != null && rpcBlock.Withdrawals.Length > 0)
                {
                    withdrawals = rpcBlock.Withdrawals.Select(w => new WithdrawalEntry(w.Address, w.Amount.Value, (ulong)w.Index.Value, (ulong)w.ValidatorIndex.Value)).ToList();
                    foreach (var w in rpcBlock.Withdrawals) { totalGwei += w.Amount.Value; if (!recipients.Contains(w.Address.ToLowerInvariant())) recipients.Add(w.Address.ToLowerInvariant()); }
                }

                // Recipient balances BEFORE this block (post previous block).
                var beforeBal = new Dictionary<string, BigInteger>();
                foreach (var r in recipients)
                    beforeBal[r] = (await state.GetAccountAsync(r))?.Balance.ToBigInteger() ?? BigInteger.Zero;

                var opts = new BlockExecutionOptions
                {
                    ReadOnly = false,
                    CaptureWitness = false,
                    ParentBeaconBlockRoot = header.ParentBeaconBlockRoot
                };

                var engine = freshPerBlock ? BuildEngine() : sharedEngine;

                // Instrument the cold descent on the target (last) block so its checkpoint-node reads can be
                // dumped for a later live-vs-checkpoint diff.
                bool record = dumpReads != null && n == to;
                if (record) { nodes.Reads.Clear(); nodes.Recording = true; }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await engine.ExecuteAsync(header, txEntries, uncles: null, withdrawals, opts);
                sw.Stop();
                if (record) nodes.Recording = false;

                if (result.Exception != null)
                {
                    Console.WriteLine($"  {n,12:N0}  EXECUTION FAILED: {result.ErrorMessage}");
                    Console.Error.WriteLine(result.Exception);
                    return 3;
                }

                bool match = result.PostStateRoot != null && header.StateRoot != null
                             && ByteUtil.AreEqual(result.PostStateRoot, header.StateRoot);
                if (!match) anyDiverged = true;

                Console.WriteLine(
                    $"  {n,12:N0}  {result.Fork,-10}  {txEntries.Count,4}  {(withdrawals?.Count ?? 0),3}  " +
                    $"{(match ? "YES " : "NO  ")}  0x{result.PostStateRoot?.ToHex()} / 0x{header.StateRoot?.ToHex()}  ({sw.Elapsed.TotalSeconds:F1}s)");

                // Withdrawal-recipient delta diagnostic.
                foreach (var r in recipients)
                {
                    var after = (await state.GetAccountAsync(r))?.Balance.ToBigInteger() ?? BigInteger.Zero;
                    var delta = after - beforeBal[r];
                    Console.WriteLine($"        recipient {r}: balanceBefore={beforeBal[r]} balanceAfter={after} delta={delta} wei");
                }
                if (recipients.Count > 0)
                    Console.WriteLine($"        withdrawals totalGwei={totalGwei} => required delta = {totalGwei * 1_000_000_000} wei");

                if (record)
                {
                    // Distinct checkpoint-node reads ('B' = served from the checkpoint base) during this block's
                    // cold descent, grouped by owner (empty owner = account trie; else keccak(addr) storage trie).
                    var baseReads = nodes.Reads.Where(x => x.Src == 'B')
                        .Select(x => (x.Owner, x.Path)).Distinct().ToList();
                    var byOwner = baseReads.GroupBy(x => x.Owner).OrderByDescending(g => g.Count()).ToList();
                    var lines = new List<string>
                    {
                        $"# block {n} cold descent: checkpoint-base node reads (owner,path)",
                        $"# total distinct base reads = {baseReads.Count}; account-trie owner = (empty); storage owner = keccak(addr)",
                        $"# distinct storage owners touched = {byOwner.Count(g => g.Key.Length > 0)}"
                    };
                    foreach (var g in byOwner)
                    {
                        lines.Add($"OWNER {(g.Key.Length == 0 ? "(account-trie)" : g.Key)}  nodes={g.Count()}");
                        foreach (var (o, p) in g.OrderBy(x => x.Path.Length).ThenBy(x => x.Path))
                            lines.Add($"  {(o.Length == 0 ? "" : o)}/{p}");
                    }
                    System.IO.File.WriteAllLines(dumpReads, lines);
                    Console.WriteLine($"        cold-descent checkpoint-node reads: {baseReads.Count} distinct (owners={byOwner.Count}) -> {dumpReads}");
                }

                if (!match)
                {
                    Console.WriteLine($"        DIVERGENCE at block {n}: computed != canonical (see per-recipient deltas above and BlockExecutor DIVERGENCE log).");
                    // Continue is pointless after a discard on a live-style follower; stop at first divergence.
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine(anyDiverged ? "  RESULT: divergence reproduced offline." : "  RESULT: all blocks matched canonical.");
            return anyDiverged ? 10 : 0;
        }

        // Geth on the NUC is resource-constrained and stalls under load; retry transient RPC failures.
        private static async Task<T> Retry<T>(Func<Task<T>> op, int attempts = 20)
        {
            Exception last = null;
            for (int a = 0; a < attempts; a++)
            {
                try { return await op(); }
                catch (Exception ex) when (
                    ex is RpcClientTimeoutException
                    || ex is System.Net.Http.HttpRequestException
                    || ex is TaskCanceledException
                    || ex is Nethereum.JsonRpc.Client.RpcResponseException   // geth-side "request timed out" under NUC load
                    || ex is System.IO.IOException)
                {
                    last = ex;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(45, 5 * (a + 1))));
                }
            }
            throw new Exception($"RPC failed after {attempts} attempts: {last?.Message}", last);
        }

    }
}
