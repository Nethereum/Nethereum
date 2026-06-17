using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using NewtonsoftFormatting = Newtonsoft.Json.Formatting;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Per-block regression cells for historic mainnet divergences. Each
    /// cell pins a block we already fixed (or one we haven't yet) by
    /// seeding only the accounts the block actually touches, replaying it
    /// through <see cref="BlockExecutor"/>, and asserting per-account
    /// post-state. This is the Tier-1 protection layer: enough to catch
    /// the kind of regression that would have prevented the failed
    /// CallGasCosts edit from masquerading as a passing change.
    ///
    /// Each fixture lives in <c>Fixtures/MainnetBlocks/block-NNNNN.json</c>
    /// and contains the block header (RLP), transactions (RLP), uncles
    /// (RLP), pre-state for touched accounts, and post-state assertions.
    /// </summary>
    public class MainnetBlockReplayTests
    {
        private readonly ITestOutputHelper _output;
        public MainnetBlockReplayTests(ITestOutputHelper output) { _output = output; }

        [Theory]
        [Trait("Category", "MainnetBlockCell")]
        // Active cells — pass with the current sender+coinbase+to+precompiles
        // pre-state capture. Adding rows here protects these historic
        // divergences against any future EVM regression.
        [InlineData(49439, "block-49439.json")]
        [InlineData(57257, "block-57257.json")]
        // Cells that need richer pre-state than the Tier 1 emitter currently
        // captures (e.g. contracts called by sub-CALLs whose state isn't in
        // sender+to+coinbase+precompiles). To activate, either improve the
        // emitter to also pre-capture the access-list addresses BEFORE block
        // run (two-pass approach), or extend to Tier 2 (witness + state root).
        // The existing FrontierLegacyTests SweepFork_* covers the same bug
        // patterns at the fork-test level, so these cells are a nice-to-have
        // not a hole in regression coverage.
        // [InlineData(51921, "block-51921.json")]   // SELFDESTRUCT-empty
        // [InlineData(55296, "block-55296.json")]   // IDENTITY precompile pattern
        // [InlineData(62509, "block-62509.json")]   // BLOCKHASH lottery
        // [InlineData(116525, "block-116525.json")] // storage roots
        // 314115 / 505137 — fixtures pending (fixture-gen sync stalled at peer
        // pool exhaustion past 116k). Generate by re-running fixture-gen with
        // a healthier peer pool, then activate.
        // [InlineData(314115, "block-314115.json")]
        // [InlineData(505137, "block-505137.json")]
        public async Task MainnetBlock_PostStateMatchesAssertions(long blockNumber, string fixtureFile)
        {
            var fixture = LoadFixture(fixtureFile);
            Assert.Equal(blockNumber, fixture.BlockNumber);

            var stateStore = new InMemoryStateStore();
            var trieStore = new InMemoryTrieNodeStore();
            var blockStore = new InMemoryBlockStore();

            await SeedPreStateAsync(stateStore, fixture.PreState);

            var calculator = new IncrementalStateRootCalculator(stateStore, trieStore);
            var executor = new BlockExecutor(
                stateStore,
                blockStore,
                MainnetChainActivations.Instance,
                chainConfigFactory: f => new ChainConfig
                {
                    ChainId = MainnetGenesisConstants.ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Hardfork = f.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: f => DefaultMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calculator,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieStore);

            var header = BuildHeader(fixture.Header);
            var transactions = fixture.TransactionsRlp
                .Select(rlp => TransactionFactory.CreateTransaction(rlp.HexToByteArray()))
                .Cast<ISignedTransaction>()
                .ToList();
            var uncles = (fixture.Uncles ?? new List<MainnetBlockHeaderFixture>())
                .Select(BuildHeader)
                .ToList();
            var txEntries = transactions.Select(t => new TxEntry(t, null)).ToList();

            var result = await executor.ExecuteAsync(header, txEntries, uncles, withdrawals: null, new BlockExecutionOptions());

            _output.WriteLine($"Block {fixture.BlockNumber}: {fixture.Scenario}");
            _output.WriteLine($"  fork={result.Fork}, txs={result.Receipts.Count}, " +
                              $"miner+={result.MinerRewardCredited}, error={result.ErrorMessage ?? "(none)"}");

            // Tier 1 fixtures seed only the touched accounts, so the global
            // state root computed over that partial trie will NOT match the
            // canonical header.StateRoot — the engine reports that as
            // StateRootMismatch. That's expected. We don't assert root match
            // here. (Tier 2 fixtures with Patricia witness proofs would.)
            // We only fail if the engine threw a hard execution exception
            // (tx execution exception, missing precompile, etc.).
            if (result.Exception != null)
            {
                Assert.True(false, $"Engine errored: {result.ErrorMessage}");
            }

            foreach (var (address, expected) in fixture.PostAssertions)
            {
                await AssertAccountAsync(stateStore, address.ToLowerInvariant(), expected);
            }
        }

        /// <summary>
        /// Per-block trace-diff harness. Replays the same fixture as
        /// <see cref="MainnetBlock_PostStateMatchesAssertions"/> through both our
        /// <see cref="TransactionExecutor"/> (with trace capture) and geth's
        /// <c>evm.exe t8n</c> tool (with <c>--trace</c>), prints first opcode
        /// divergence per transaction with context window, plus per-account
        /// post-state diff between geth and Nethereum. Used by the
        /// <c>/evm-debug</c> skill to bisect mainnet-replay regressions to a
        /// single PC/opcode and source line.
        /// </summary>
        [Theory]
        [Trait("Category", "MainnetBlockCell-Trace")]
        [MemberData(nameof(DiscoverMainnetBlockFixtures))]
        public async Task Debug_BlockReplay_DiffWithGethT8n(ulong blockNumber, string fixtureFile, string fork)
        {
            var fixture = LoadFixture(fixtureFile);
            Assert.Equal((long)blockNumber, fixture.BlockNumber);

            _output.WriteLine($"=== Debug_BlockReplay_DiffWithGethT8n block={blockNumber} file={fixtureFile} fork={fork} ===");

            var header = BuildHeader(fixture.Header);
            var transactions = fixture.TransactionsRlp
                .Select(rlp => TransactionFactory.CreateTransaction(rlp.HexToByteArray()))
                .Cast<ISignedTransaction>()
                .ToList();

            var keccak = new Sha3Keccack();
            var blockHashBytes = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
            var blockHashHex = "0x" + blockHashBytes.ToHex();
            _output.WriteLine($"Computed blockHash: {blockHashHex}");
            _output.WriteLine($"Header stateRoot:   {fixture.Header.StateRoot}");
            _output.WriteLine($"Header gasUsed:     {fixture.Header.GasUsed}");
            _output.WriteLine($"Tx count:           {transactions.Count}");

            // 1. Seed a fresh in-memory state store with the fixture pre-state, run our
            //    BlockExecutor to obtain canonical per-tx and post-state results.
            var stateStore = new InMemoryStateStore();
            var trieStore = new InMemoryTrieNodeStore();
            var blockStore = new InMemoryBlockStore();
            await SeedPreStateAsync(stateStore, fixture.PreState);

            var calculator = new IncrementalStateRootCalculator(stateStore, trieStore);
            var executor = new BlockExecutor(
                stateStore,
                blockStore,
                MainnetChainActivations.Instance,
                chainConfigFactory: f => new ChainConfig
                {
                    ChainId = MainnetGenesisConstants.ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Hardfork = f.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: f => DefaultMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calculator,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieStore);

            var txEntries = transactions.Select(t => new TxEntry(t, null)).ToList();
            var processorResult = await executor.ExecuteAsync(header, txEntries, new List<BlockHeader>(), withdrawals: null, new BlockExecutionOptions());
            _output.WriteLine($"Nethereum BlockExecutor: fork={processorResult.Fork} txs={processorResult.Receipts.Count} " +
                              $"minerCredit={processorResult.MinerRewardCredited} computedStateRoot={processorResult.PostStateRoot?.ToHex(true) ?? "(null)"} " +
                              $"error={processorResult.ErrorMessage ?? "(none)"}");

            // 2. Write geth t8n inputs (alloc/env/txs.rlp) and invoke evm.exe.
            var tmpDir = Path.Combine(Path.GetTempPath(), "neth_block_t8n_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tmpDir);
            _output.WriteLine($"t8n tmp dir: {tmpDir}");

            var alloc = BuildAllocFromPreState(fixture.PreState);
            var env = BuildEnvFromHeader(fixture.Header);
            File.WriteAllText(Path.Combine(tmpDir, "alloc.json"), alloc.ToString(NewtonsoftFormatting.None));
            File.WriteAllText(Path.Combine(tmpDir, "env.json"), env.ToString(NewtonsoftFormatting.None));

            // Geth t8n's .rlp path reads the file via json.Unmarshal into a hexutil.Bytes,
            // i.e. the file must contain a JSON-quoted hex string like "0x..." that decodes
            // to the RLP list of signed txs — NOT the raw RLP bytes themselves.
            var txItems = fixture.TransactionsRlp.Select(r => r.HexToByteArray()).ToArray();
            var txsRlp = Nethereum.RLP.RLP.EncodeList(txItems);
            var txsRlpHexJson = "\"0x" + txsRlp.ToHex() + "\"";
            File.WriteAllText(Path.Combine(tmpDir, "txs.rlp"), txsRlpHexJson);

            var outDir = Path.Combine(tmpDir, "out");
            Directory.CreateDirectory(outDir);

            var evmExe = FindEvmExe();
            // --state.reward defaults to 0 in t8n — without it, geth's post-state excludes
            // the consensus miner reward and the coinbase diff looks like an EVM bug.
            // We resolve the reward from the same BlockRewardCalculator the BlockExecutor uses.
            var forkActivations = Nethereum.EVM.HardforkNames.Parse(fork);
            var minerReward = Nethereum.CoreChain.BlockRewardCalculator.MinerReward(forkActivations);
            var args = $"t8n --state.fork={fork} --state.reward={minerReward} " +
                       $"--input.alloc={Path.Combine(tmpDir, "alloc.json")} " +
                       $"--input.env={Path.Combine(tmpDir, "env.json")} --input.txs={Path.Combine(tmpDir, "txs.rlp")} " +
                       $"--output.basedir={outDir} --output.alloc=alloc.json --output.result=result.json " +
                       $"--trace --trace.memory --trace.returndata";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = evmExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = tmpDir
            };
            string t8nStderr = string.Empty;
            int t8nExit;
            using (var proc = new System.Diagnostics.Process { StartInfo = psi })
            {
                proc.Start();
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                // 10 min — full --trace --trace.memory --trace.returndata on
                // post-Cancun mainnet blocks (134+ txs) emits multi-GB of trace
                // JSONL and easily exceeds a 2-minute budget on a non-fast disk.
                var completed = await Task.Run(() => proc.WaitForExit(600000));
                if (!completed)
                {
                    try { proc.Kill(); } catch { }
                    _output.WriteLine("t8n timeout");
                    Assert.True(false, "geth t8n timed out — inspect tmp dir " + tmpDir);
                }
                t8nStderr = await stderrTask;
                _ = await stdoutTask;
                t8nExit = proc.ExitCode;
            }

            if (t8nExit != 0)
            {
                _output.WriteLine($"geth t8n exited {t8nExit}");
                _output.WriteLine("--- stderr (last 20 lines) ---");
                foreach (var line in TailLines(t8nStderr, 20)) _output.WriteLine(line);
                Assert.True(false, $"geth t8n failed (exit={t8nExit}). Tmp dir kept at {tmpDir}");
            }

            // 3. Parse geth's result.json + alloc.json to get state root, gas used, post-state.
            var gethResultPath = Path.Combine(outDir, "result.json");
            var gethAllocPath = Path.Combine(outDir, "alloc.json");
            Assert.True(File.Exists(gethResultPath), $"missing geth result.json at {gethResultPath}");
            Assert.True(File.Exists(gethAllocPath), $"missing geth alloc.json at {gethAllocPath}");

            var gethResult = JObject.Parse(File.ReadAllText(gethResultPath));
            var gethStateRoot = (string)gethResult["stateRoot"];
            var gethGasUsed = (string)gethResult["gasUsed"];
            _output.WriteLine($"geth stateRoot: {gethStateRoot}");
            _output.WriteLine($"geth gasUsed:   {gethGasUsed}");

            var gethRejected = gethResult["rejected"] as JArray;
            if (gethRejected != null && gethRejected.Count > 0)
            {
                _output.WriteLine($"geth rejected {gethRejected.Count} txs:");
                foreach (var r in gethRejected) _output.WriteLine($"  {r}");
            }
            var gethReceipts = gethResult["receipts"] as JArray ?? new JArray();
            for (int i = 0; i < gethReceipts.Count; i++)
            {
                var r = gethReceipts[i];
                _output.WriteLine($"  tx[{i}] geth: status={r["status"]} cumulativeGasUsed={r["cumulativeGasUsed"]} txHash={r["transactionHash"]}");
            }

            // 4. Per-tx: replay each tx through our TransactionExecutor with trace capture
            //    on a freshly-seeded ExecutionStateService. Compare per-tx step counts and
            //    first divergence against geth's per-tx jsonl file.
            EnsureHardforkConfigForFork(fork, out var hardforkConfig);
            var verifier = new TransactionVerificationAndRecoveryImp();
            var traceComparer = new TraceComparer();

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var txHash = "0x" + tx.Hash.ToHex();
                _output.WriteLine($"--- tx[{i}] hash={txHash} ---");

                var ourTrace = await CaptureTraceForTransactionAsync(tx, header, fixture.PreState, hardforkConfig, verifier);
                _output.WriteLine($"  nethereum: success={ourTrace.Success} gasUsed={ourTrace.GasUsed} " +
                                  $"revert='{ourTrace.RevertReason ?? string.Empty}' steps={ourTrace.Steps?.Count ?? 0} error='{ourTrace.Error ?? string.Empty}'");

                var gethTraceFile = Directory.GetFiles(outDir, $"trace-{i}-*.jsonl").FirstOrDefault();
                if (gethTraceFile == null)
                {
                    _output.WriteLine($"  geth trace file missing for tx[{i}] — t8n likely rejected this tx (see above)");
                    continue;
                }
                _output.WriteLine($"  geth trace file: {Path.GetFileName(gethTraceFile)}");
                var gethLines = File.ReadAllLines(gethTraceFile);
                var gethSteps = ParseGethTraceJsonl(gethLines);
                _output.WriteLine($"  geth: steps={gethSteps.Count}");

                if (ourTrace.Steps == null || ourTrace.Steps.Count == 0)
                {
                    _output.WriteLine("  Nethereum produced no opcode steps (validation error or call to precompile?) — cannot diff trace.");
                    continue;
                }

                var cmp = traceComparer.Compare(gethSteps, ourTrace.Steps);
                if (cmp.FirstDivergenceStep < 0)
                {
                    _output.WriteLine($"  TRACE MATCH ({cmp.MatchingSteps}/{cmp.GethStepCount} steps)");
                }
                else
                {
                    _output.WriteLine($"  TRACE DIVERGE at step {cmp.FirstDivergenceStep} ({cmp.DivergenceReason})");
                    _output.WriteLine(cmp.DivergenceDetails);
                    PrintTraceWindow(_output, cmp, contextBefore: 5, contextAfter: 5);
                }
            }

            // 5. Per-account post-state diff: geth post-alloc vs Nethereum stateStore.
            var gethAlloc = JObject.Parse(File.ReadAllText(gethAllocPath));
            _output.WriteLine($"=== Post-state DIFF (geth vs nethereum) — {gethAlloc.Count} accounts in geth post-state ===");
            int diffCount = 0;
            foreach (var p in gethAlloc.Properties())
            {
                var addr = p.Name.ToLowerInvariant();
                var gethAcc = (JObject)p.Value;
                var gethBalance = NormalizeHexNumber((string)gethAcc["balance"] ?? "0x0");
                var gethNonceStr = NormalizeHexNumber((string)gethAcc["nonce"] ?? "0x0");
                var gethCode = (string)gethAcc["code"] ?? "0x";
                var gethStorage = gethAcc["storage"] as JObject;

                var ourAccount = await stateStore.GetAccountAsync(addr);
                if (ourAccount == null)
                {
                    _output.WriteLine($"  {addr}: geth balance={gethBalance} nonce={gethNonceStr} codeLen={Math.Max(0, gethCode.Length - 2) / 2}  |  neth=(missing)");
                    diffCount++;
                    continue;
                }
                var ourBalance = NormalizeHexNumber("0x" + new BigInteger(ourAccount.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x"));
                var ourNonce = NormalizeHexNumber("0x" + new BigInteger(ourAccount.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true).ToString("x"));
                var ourCodeBytes = ourAccount.CodeHash != null && !ByteUtil.AreEqual(ourAccount.CodeHash, DefaultValues.EMPTY_DATA_HASH)
                    ? await stateStore.GetCodeAsync(ourAccount.CodeHash) ?? Array.Empty<byte>()
                    : Array.Empty<byte>();
                var ourCode = ourCodeBytes.Length == 0 ? "0x" : "0x" + ourCodeBytes.ToHex();

                bool balanceMatch = string.Equals(ourBalance, gethBalance, StringComparison.OrdinalIgnoreCase);
                bool nonceMatch = string.Equals(ourNonce, gethNonceStr, StringComparison.OrdinalIgnoreCase);
                bool codeMatch = string.Equals(ourCode, gethCode, StringComparison.OrdinalIgnoreCase);

                var sb = new StringBuilder();
                if (!balanceMatch) sb.Append($" balance(geth={gethBalance} neth={ourBalance})");
                if (!nonceMatch) sb.Append($" nonce(geth={gethNonceStr} neth={ourNonce})");
                if (!codeMatch) sb.Append($" code(geth-len={Math.Max(0, gethCode.Length - 2) / 2} neth-len={ourCodeBytes.Length})");

                if (gethStorage != null)
                {
                    foreach (var s in gethStorage.Properties())
                    {
                        var slotHex = s.Name;
                        var slotBI = new HexBigInteger(slotHex).Value;
                        var gethSlotVal = NormalizeHexNumber((string)s.Value ?? "0x0");
                        var ourSlotBytes = await stateStore.GetStorageAsync(addr, slotBI) ?? Array.Empty<byte>();
                        var ourSlotVal = ourSlotBytes.Length == 0 ? "0x0" : NormalizeHexNumber("0x" + ourSlotBytes.ToHex());
                        if (!string.Equals(gethSlotVal, ourSlotVal, StringComparison.OrdinalIgnoreCase))
                            sb.Append($" storage[{slotHex}](geth={gethSlotVal} neth={ourSlotVal})");
                    }
                }

                if (sb.Length > 0)
                {
                    _output.WriteLine($"  {addr}:{sb}");
                    diffCount++;
                }
            }

            _output.WriteLine($"=== Diff summary: {diffCount} accounts differ between geth and Nethereum ===");

            // Compare top-level state roots. We expect this to fail for known divergences
            // (which is the harness's purpose), but the diagnostic output above runs first.
            Assert.False(string.IsNullOrEmpty(gethStateRoot), "geth produced no stateRoot");
            if (processorResult.PostStateRoot != null)
            {
                var ourStateRoot = processorResult.PostStateRoot.ToHex(true);
                Assert.Equal(gethStateRoot.ToLowerInvariant(), ourStateRoot.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Scans <c>tests/Nethereum.EVM.UnitTests/Fixtures/MainnetBlocks/</c>
        /// for <c>block-N.json</c> at test-discovery time, infers the active
        /// fork from each fixture's header number+timestamp via
        /// <see cref="MainnetChainActivations"/>, and emits one Theory row per
        /// fixture. Newly emitted fixtures (auto-written by
        /// <c>FixtureEmittingBlockExecutor</c> on a sync divergence) flow
        /// straight into the next test run as a fresh regression cell — no
        /// hand-maintained InlineData list to keep in sync.
        /// </summary>
        public static IEnumerable<object[]> DiscoverMainnetBlockFixtures()
        {
            var dir = FindFixtureDirectory();
            if (dir == null || !Directory.Exists(dir)) yield break;

            foreach (var file in Directory.GetFiles(dir, "block-*.json"))
            {
                var name = Path.GetFileName(file);
                var numText = Path.GetFileNameWithoutExtension(name);
                if (!numText.StartsWith("block-", StringComparison.Ordinal)) continue;
                if (!ulong.TryParse(numText.Substring("block-".Length), out var blockNumber)) continue;

                string fork;
                try
                {
                    var fixture = JsonSerializer.Deserialize<MainnetBlockFixture>(File.ReadAllText(file),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (fixture?.Header == null) continue;
                    ulong timestamp = (ulong)ParseUnsignedHex(fixture.Header.Timestamp);
                    var hardfork = MainnetChainActivations.Instance.ResolveAt((long)blockNumber, timestamp);
                    fork = ToGethForkName(hardfork);
                }
                catch
                {
                    continue;
                }

                yield return new object[] { blockNumber, name, fork };
            }
        }

        /// <summary>
        /// Geth's t8n <c>--state.fork</c> only accepts the EVM-behaviour
        /// hardforks. Forks that ship no EVM/gas changes (FrontierThawing,
        /// DaoFork header rule, MuirGlacier/ArrowGlacier/GrayGlacier difficulty
        /// bomb delays) alias to the nearest EVM-equivalent fork so the test
        /// can still drive geth t8n. Mirrors the rule in
        /// <see cref="MainnetChainActivations"/>.
        /// </summary>
        private static string ToGethForkName(HardforkName fork)
        {
            switch (fork)
            {
                case HardforkName.FrontierThawing: return "Frontier";
                case HardforkName.DaoFork:         return "Homestead";
                case HardforkName.MuirGlacier:     return "Istanbul";
                case HardforkName.ArrowGlacier:    return "London";
                case HardforkName.GrayGlacier:     return "London";
                default:                            return fork.ToString();
            }
        }

        private static string FindFixtureDirectory()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                var candidate = Path.Combine(
                    current.FullName, "tests", "Nethereum.EVM.UnitTests", "Fixtures", "MainnetBlocks");
                if (Directory.Exists(candidate)) return candidate;
                current = current.Parent;
            }
            return null;
        }

        private static JObject BuildAllocFromPreState(Dictionary<string, MainnetAccountFixture> preState)
        {
            // Geth t8n requires 32-byte (64-hex-char) storage keys and values; raw
            // fixture entries like "0x1" or "0x0af2..." (65 hex chars from a
            // leading-zero artefact) are rejected. Strip leading zeros, then
            // left-pad back to 64 hex chars.
            static string Pad32(string s)
            {
                if (string.IsNullOrEmpty(s)) return "0x" + new string('0', 64);
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
                s = s.ToLowerInvariant().TrimStart('0');
                if (s.Length == 0) return "0x" + new string('0', 64);
                if (s.Length > 64) throw new ArgumentException($"storage key/value > 32 bytes after trim: {s}");
                return "0x" + new string('0', 64 - s.Length) + s;
            }

            var alloc = new JObject();
            foreach (var (rawAddr, acc) in preState)
            {
                var addr = rawAddr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? rawAddr.ToLowerInvariant()
                    : "0x" + rawAddr.ToLowerInvariant();
                var entry = new JObject
                {
                    ["balance"] = string.IsNullOrEmpty(acc.Balance) ? "0x0" : acc.Balance,
                    ["nonce"] = string.IsNullOrEmpty(acc.Nonce) ? "0x0" : acc.Nonce,
                    ["code"] = string.IsNullOrEmpty(acc.Code) ? "0x" : acc.Code
                };
                if (acc.Storage != null && acc.Storage.Count > 0)
                {
                    var storage = new JObject();
                    foreach (var (slot, value) in acc.Storage) storage[Pad32(slot)] = Pad32(value);
                    entry["storage"] = storage;
                }
                alloc[addr] = entry;
            }
            return alloc;
        }

        private static JObject BuildEnvFromHeader(MainnetBlockHeaderFixture h)
        {
            // Geth t8n env uses uint64 hex without leading zeros for numeric fields.
            string Canonical(string s)
            {
                if (string.IsNullOrEmpty(s)) return "0x0";
                var bi = new HexBigInteger(s).Value;
                if (bi.IsZero) return "0x0";
                var hex = bi.ToString("x").TrimStart('0');
                return hex.Length == 0 ? "0x0" : "0x" + hex;
            }

            var env = new JObject
            {
                ["currentCoinbase"] = h.Coinbase.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? h.Coinbase
                    : "0x" + h.Coinbase,
                ["currentDifficulty"] = Canonical(h.Difficulty),
                ["currentGasLimit"] = Canonical(h.GasLimit),
                ["currentNumber"] = Canonical(h.Number),
                ["currentTimestamp"] = Canonical(h.Timestamp),
                ["currentRandom"] = h.MixHash ?? "0x0000000000000000000000000000000000000000000000000000000000000000"
            };
            if (!string.IsNullOrEmpty(h.BaseFee)) env["currentBaseFee"] = Canonical(h.BaseFee);
            if (!string.IsNullOrEmpty(h.ParentBeaconBlockRoot)) env["parentBeaconBlockRoot"] = h.ParentBeaconBlockRoot;
            // Shanghai+ requires the withdrawals field in t8n env, even when empty.
            // We don't model per-block withdrawals in the per-tx trace-diff harness
            // (withdrawals only affect post-state balances, not opcode execution).
            if (!string.IsNullOrEmpty(h.WithdrawalsRoot)) env["withdrawals"] = new JArray();
            // BLOCKHASH lookback support: only the parent hash is needed for tests run
            // single-block. t8n accepts a blockHashes map keyed by block number.
            if (!string.IsNullOrEmpty(h.ParentHash))
            {
                var n = new HexBigInteger(h.Number).Value - 1;
                if (n >= 0)
                {
                    env["blockHashes"] = new JObject { [n.ToString()] = h.ParentHash };
                }
            }
            return env;
        }

        private static string FindEvmExe()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln"))) break;
                dir = dir.Parent;
            }
            if (dir == null) throw new DirectoryNotFoundException("project root not found");
            var gethToolsDir = Path.Combine(dir.FullName, "geth-tools");
            var evms = Directory.GetFiles(gethToolsDir, "evm.exe", SearchOption.AllDirectories);
            if (evms.Length == 0) throw new FileNotFoundException("evm.exe not found under geth-tools");
            return evms[0];
        }

        private static void EnsureHardforkConfigForFork(string fork, out HardforkConfig config)
        {
            var hardforkName = HardforkNames.Parse(fork);
            config = DefaultMainnetHardforkRegistry.Instance.Get(hardforkName);
        }

        // Per-tx replay: rebuild ExecutionStateService from the fixture pre-state,
        // build a TransactionExecutionContext from the parsed ISignedTransaction +
        // block header, invoke TransactionExecutor with TraceEnabled=true. Each call
        // gets a fresh state — this is intentional: we are diffing the OPCODE trace
        // per-tx, not chaining state through the block.
        private async Task<PerTxTraceCapture> CaptureTraceForTransactionAsync(
            ISignedTransaction tx,
            BlockHeader header,
            Dictionary<string, MainnetAccountFixture> preState,
            HardforkConfig hardforkConfig,
            TransactionVerificationAndRecoveryImp verifier)
        {
            var capture = new PerTxTraceCapture();
            try
            {
                var executionState = new ExecutionStateService(new MockNodeDataService());
                foreach (var (rawAddr, acc) in preState)
                {
                    var addr = rawAddr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? rawAddr.ToLowerInvariant() : "0x" + rawAddr.ToLowerInvariant();
                    var st = executionState.CreateOrGetAccountExecutionState(addr);
                    st.WasInPreState = true;
                    st.Code = string.IsNullOrEmpty(acc.Code) || acc.Code == "0x" ? Array.Empty<byte>() : acc.Code.HexToByteArray();
                    st.Balance.SetInitialChainBalance(
                        EvmUInt256BigIntegerExtensions.FromBigInteger(string.IsNullOrEmpty(acc.Balance) ? BigInteger.Zero : new HexBigInteger(acc.Balance).Value));
                    st.Nonce = string.IsNullOrEmpty(acc.Nonce) ? (ulong?)0 : (ulong)new HexBigInteger(acc.Nonce).Value;
                    if (acc.Storage != null)
                    {
                        foreach (var (slotHex, valueHex) in acc.Storage)
                        {
                            var k = EvmUInt256BigIntegerExtensions.FromBigInteger(new HexBigInteger(slotHex).Value);
                            st.SetPreStateStorage(k, valueHex.HexToByteArray());
                        }
                    }
                }
                executionState.MarkPrecompilesAsWarm(hardforkConfig.Precompiles);

                var sender = verifier.GetSenderAddress(tx);

                // Read raw legacy/typed tx fields. block-55239 is a legacy tx so we
                // use SignedLegacyTransaction accessors. For typed txs (1559/2930/4844/
                // 7702) we'd need to branch — out of scope for this initial harness
                // which targets the existing Frontier-era fixtures.
                var legacy = tx as SignedLegacyTransaction
                    ?? throw new NotSupportedException("Only SignedLegacyTransaction supported in initial Debug_BlockReplay_DiffWithGethT8n harness");

                BigInteger ToUnsigned(byte[] b) => b == null || b.Length == 0 ? BigInteger.Zero : new BigInteger(b, isUnsigned: true, isBigEndian: true);
                var gasLimit = ToUnsigned(legacy.GasLimit);
                var gasPrice = ToUnsigned(legacy.GasPrice);
                var value = ToUnsigned(legacy.Value);
                var nonce = (ulong)ToUnsigned(legacy.Nonce);
                var to = legacy.ReceiveAddress != null && legacy.ReceiveAddress.Length == 20
                    ? "0x" + legacy.ReceiveAddress.ToHex()
                    : null;
                var data = legacy.Data ?? Array.Empty<byte>();

                EvmUInt256 ToU256(BigInteger b) => EvmUInt256BigIntegerExtensions.FromBigInteger(b);

                var ctx = new TransactionExecutionContext
                {
                    Sender = sender.ToLowerInvariant(),
                    To = to?.ToLowerInvariant(),
                    Data = data,
                    GasLimit = (long)gasLimit,
                    Value = ToU256(value),
                    GasPrice = ToU256(gasPrice),
                    Nonce = nonce,
                    IsEip1559 = false,
                    IsContractCreation = to == null,
                    BlockNumber = (long)(ulong)header.BlockNumber,
                    Timestamp = header.Timestamp,
                    Coinbase = header.Coinbase,
                    BaseFee = header.BaseFee ?? EvmUInt256.Zero,
                    Difficulty = header.Difficulty,
                    BlockGasLimit = header.GasLimit,
                    ChainId = EvmUInt256.One,
                    ExecutionState = executionState,
                    TraceEnabled = true
                };

                var executor = new TransactionExecutor(hardforkConfig);
                var result = await executor.ExecuteAsync(ctx);

                capture.Success = result.Success;
                capture.GasUsed = result.GasUsed;
                capture.RevertReason = result.RevertReason;
                capture.Error = result.Error;

                if (result.Traces != null)
                {
                    var comparer = new TraceComparer();
                    capture.Steps = comparer.NormalizeNethTrace(result.Traces);
                }
            }
            catch (Exception ex)
            {
                capture.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return capture;
        }

        // Geth t8n's jsonl trace emits gas/gasCost as hex strings (e.g. "0x10c78"),
        // and "op" as a numeric opcode byte rather than a name — the canonical name
        // is in "opName". TraceComparer.ParseGethTraceLines targets statetest-style
        // output where these are decimal/strings and skips lines whose Value<long>
        // call throws. Parse manually here to handle both shapes.
        private static List<GethTraceStep> ParseGethTraceJsonl(string[] lines)
        {
            static long ParseHexOrLong(JToken t)
            {
                if (t == null) return 0;
                if (t.Type == JTokenType.Integer) return t.Value<long>();
                var v = t.Value<string>();
                if (string.IsNullOrEmpty(v)) return 0;
                if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt64(v.Substring(2), 16);
                return long.TryParse(v, out var r) ? r : 0;
            }

            var steps = new List<GethTraceStep>();
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!raw.TrimStart().StartsWith("{")) continue;
                JObject obj;
                try { obj = JObject.Parse(raw); } catch { continue; }
                if (obj["pc"] == null) continue;
                var stack = obj["stack"] as JArray;
                var memory = obj["memory"] as JArray;
                var storage = obj["storage"] as JObject;
                steps.Add(new GethTraceStep
                {
                    PC = obj["pc"]?.Value<int>() ?? 0,
                    Op = obj["opName"]?.Value<string>() ?? obj["op"]?.ToString() ?? string.Empty,
                    Gas = ParseHexOrLong(obj["gas"]),
                    GasCost = ParseHexOrLong(obj["gasCost"]),
                    Depth = obj["depth"]?.Value<int>() ?? 1,
                    Error = obj["error"]?.Value<string>(),
                    Stack = stack?.Select(s => s.Value<string>() ?? string.Empty).ToList() ?? new List<string>(),
                    Memory = memory != null ? string.Join(string.Empty, memory.Select(m => (m.Value<string>() ?? string.Empty).Replace("0x", string.Empty))) : (obj["memory"]?.Value<string>() ?? string.Empty),
                    Storage = storage != null
                        ? storage.Properties().ToDictionary(p => p.Name, p => p.Value.Value<string>() ?? string.Empty)
                        : new Dictionary<string, string>()
                });
            }
            return steps;
        }

        private static IEnumerable<string> TailLines(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length <= n ? lines : lines.Skip(lines.Length - n);
        }

        private static string NormalizeHexNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return "0x0";
            s = s.ToLowerInvariant();
            if (s.StartsWith("0x")) s = s.Substring(2);
            s = s.TrimStart('0');
            return s.Length == 0 ? "0x0" : "0x" + s;
        }

        private static void PrintTraceWindow(ITestOutputHelper output, ComparisonResult cmp, int contextBefore, int contextAfter)
        {
            int idx = cmp.FirstDivergenceStep - 1;
            int from = Math.Max(0, idx - contextBefore);
            int to = Math.Min(cmp.Steps.Count - 1, idx + contextAfter);
            output.WriteLine("  Step | Depth | PC    | Op (g/n)            | G.Gas      | N.Gas      | G.Cost | N.Cost | marker");
            output.WriteLine("  -----|-------|-------|---------------------|------------|------------|--------|--------|--------");
            for (int s = from; s <= to; s++)
            {
                var step = cmp.Steps[s];
                var marker = (s == idx) ? " >>> " : "     ";
                var opStr = string.Equals(step.GethOp, step.NethOp, StringComparison.OrdinalIgnoreCase) ? step.GethOp : $"{step.GethOp}/{step.NethOp}";
                var pcStr = step.GethPC == step.NethPC ? step.GethPC.ToString() : $"{step.GethPC}/{step.NethPC}";
                var depthStr = step.GethDepth == step.NethDepth ? step.GethDepth.ToString() : $"{step.GethDepth}/{step.NethDepth}";
                output.WriteLine($"{marker}{step.Step,5} | {depthStr,5} | {pcStr,5} | {opStr,-19} | {step.GethGas,10} | {step.NethGas,10} | {step.GethCost,6} | {step.NethCost,6} | {(step.IsMatch ? string.Empty : step.DivergenceType)}");
            }
        }

        private sealed class PerTxTraceCapture
        {
            public bool Success { get; set; }
            public long GasUsed { get; set; }
            public string RevertReason { get; set; }
            public string Error { get; set; }
            public List<NethTraceStep> Steps { get; set; }
        }

        private static async Task SeedPreStateAsync(IStateStore store, Dictionary<string, MainnetAccountFixture> preState)
        {
            var keccak = new Util.Sha3Keccack();
            foreach (var (rawAddress, fix) in preState)
            {
                var address = rawAddress.ToLowerInvariant();
                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (!string.IsNullOrEmpty(fix.Code) && fix.Code != "0x")
                {
                    var codeBytes = fix.Code.HexToByteArray();
                    codeHash = keccak.CalculateHash(codeBytes);
                    await store.SaveCodeAsync(codeHash, codeBytes);
                }
                var account = new Account
                {
                    Nonce = EvmUInt256.FromBigEndian(ParseUnsignedHex(fix.Nonce).ToByteArray(isUnsigned: true, isBigEndian: true)),
                    Balance = EvmUInt256.FromBigEndian(ParseUnsignedHex(fix.Balance).ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = codeHash
                };
                await store.SaveAccountAsync(address, account);

                if (fix.Storage != null)
                {
                    foreach (var (slot, value) in fix.Storage)
                    {
                        var slotBI = ParseUnsignedHex(slot);
                        var valBytes = value.HexToByteArray();
                        await store.SaveStorageAsync(address, slotBI, valBytes);
                    }
                }
            }
        }

        private static async Task AssertAccountAsync(IStateStore store, string address, MainnetAccountAssertion assertion)
        {
            var account = await store.GetAccountAsync(address);
            if (assertion.Exists.HasValue)
            {
                Assert.Equal(assertion.Exists.Value, account != null);
            }
            if (account == null) return;

            if (!string.IsNullOrEmpty(assertion.Balance))
            {
                var actual = new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
                var expected = ParseUnsignedHex(assertion.Balance);
                Assert.Equal(expected, actual);
            }
            if (!string.IsNullOrEmpty(assertion.Nonce))
            {
                var actual = new BigInteger(account.Nonce.ToBigEndian(), isUnsigned: true, isBigEndian: true);
                var expected = ParseUnsignedHex(assertion.Nonce);
                Assert.Equal(expected, actual);
            }
            if (!string.IsNullOrEmpty(assertion.CodeHash))
            {
                Assert.Equal(assertion.CodeHash.ToLowerInvariant(), "0x" + account.CodeHash.ToHex());
            }
            if (assertion.Storage != null)
            {
                foreach (var (slot, expectedValue) in assertion.Storage)
                {
                    var slotBI = ParseUnsignedHex(slot);
                    var actual = await store.GetStorageAsync(address, slotBI);
                    var actualHex = "0x" + (actual ?? System.Array.Empty<byte>()).ToHex();
                    Assert.Equal(expectedValue.ToLowerInvariant(), actualHex);
                }
            }
        }

        private static MainnetBlockFixture LoadFixture(string fileName)
        {
            var path = FixturePath(fileName);
            Assert.True(File.Exists(path), $"Fixture missing at {path}");
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MainnetBlockFixture>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static string FixturePath(string fileName)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "Nethereum.EVM.UnitTests", "Fixtures", "MainnetBlocks", fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "MainnetBlocks", fileName);
        }

        // Always parses hex as UNSIGNED. Using BigInteger.Parse(HexNumber) directly
        // treats high-bit-set values as negative (e.g. 0x80... → negative), which
        // breaks balances, gas, difficulty, etc. HexBigInteger handles 0x prefix +
        // unsigned semantics correctly and is the canonical helper.
        private static BigInteger ParseUnsignedHex(string s)
            => string.IsNullOrEmpty(s) ? BigInteger.Zero : new HexBigInteger(s).Value;

        private static BlockHeader BuildHeader(MainnetBlockHeaderFixture h)
        {
            byte[] HexBytes(string s) => string.IsNullOrEmpty(s) ? null : s.HexToByteArray();
            long ParseLong(string s) => (long)ParseUnsignedHex(s);
            EvmUInt256 ParseU256(string s) => EvmUInt256.FromBigEndian(
                ParseUnsignedHex(s).ToByteArray(isUnsigned: true, isBigEndian: true));

            return new BlockHeader
            {
                ParentHash = HexBytes(h.ParentHash),
                UnclesHash = HexBytes(h.UnclesHash),
                Coinbase = h.Coinbase?.ToLowerInvariant(),
                StateRoot = HexBytes(h.StateRoot),
                TransactionsHash = HexBytes(h.TransactionsRoot),
                ReceiptHash = HexBytes(h.ReceiptsRoot),
                BlockNumber = ParseU256(h.Number),
                LogsBloom = HexBytes(h.LogsBloom) ?? new byte[256],
                Difficulty = ParseU256(h.Difficulty),
                Timestamp = ParseLong(h.Timestamp),
                GasLimit = ParseLong(h.GasLimit),
                GasUsed = ParseLong(h.GasUsed),
                MixHash = HexBytes(h.MixHash) ?? new byte[32],
                ExtraData = HexBytes(h.ExtraData) ?? System.Array.Empty<byte>(),
                Nonce = HexBytes(h.Nonce) ?? new byte[8],
                BaseFee = string.IsNullOrEmpty(h.BaseFee) ? null : (EvmUInt256?)ParseU256(h.BaseFee),
                WithdrawalsRoot = HexBytes(h.WithdrawalsRoot),
                ParentBeaconBlockRoot = HexBytes(h.ParentBeaconBlockRoot)
            };
        }
    }
}
