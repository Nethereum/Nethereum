using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class RpcWitnessE2ETests
    {
        private readonly ITestOutputHelper _output;

        private static string RpcUrl =>
            Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL")
            ?? "https://mainnet.infura.io/v3/206cfadcef274b49a3a15c45c285211c";

        public RpcWitnessE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static void AssertUsableStateRoot(string side, string rootHex, System.Numerics.BigInteger blockNumber)
        {
            const string ZeroRoot = "0x0000000000000000000000000000000000000000000000000000000000000000";

            Assert.True(!string.IsNullOrEmpty(rootHex) && rootHex != "<null>",
                $"Block {blockNumber}: {side} produced NO post-state root (null/empty). "
                + "The parity comparison cannot run, and a comparison that cannot run "
                + "must fail rather than pass.");

            Assert.True(rootHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && rootHex.Length == 66,
                $"Block {blockNumber}: {side} post-state root is not a 32-byte 0x-prefixed hash: "
                + $"'{rootHex}' (length {rootHex.Length}). A shape mismatch would otherwise be "
                + "misreported as a state divergence.");

            Assert.True(!string.Equals(rootHex, ZeroRoot, StringComparison.OrdinalIgnoreCase),
                $"Block {blockNumber}: {side} reported the all-zero sentinel post-state root "
                + $"({ZeroRoot}), which is what both executors emit when no root was computed. "
                + "Treating that as a value would let the parity gate pass vacuously.");
        }

        [Fact]
        public async Task FetchBlock_RecordWitness_ReExecute_VerifyStateRoot()
        {
            var parityGateRan = false;

            var web3 = new Nethereum.Web3.Web3(RpcUrl);
            var chainId = (await web3.Eth.ChainId.SendRequestAsync()).Value;
            _output.WriteLine($"Chain ID: {chainId}");

            var latestBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var targetBlock = latestBlockNumber.Value - 5;
            _output.WriteLine($"Target block: {targetBlock}");

            var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(targetBlock)));

            Assert.NotNull(block);
            Assert.True(block.Transactions.Length > 0, "Block has no transactions");

            _output.WriteLine($"Block {block.Number.Value}: {block.Transactions.Length} transactions, gasUsed={block.GasUsed.Value}");

            int maxTx = Math.Min(3, block.Transactions.Length);

            var previousBlock = new BlockParameter(new HexBigInteger(targetBlock - 1));
            var rpcState = new RpcNodeDataService(web3.Eth, previousBlock);
            var recorder = new WitnessRecordingStateReader(rpcState);
            var executionState = new ExecutionStateService(recorder);

            var config = DefaultHardforkConfigs.Prague;
            var executor = new TransactionExecutor(config);

            var baseFee = block.BaseFeePerGas != null ? (long)block.BaseFeePerGas.Value : 0;
            var blockData = new BlockWitnessData
            {
                BlockNumber = (long)block.Number.Value,
                Timestamp = (long)block.Timestamp.Value,
                BaseFee = baseFee,
                BlockGasLimit = (long)block.GasLimit.Value,
                ChainId = (long)chainId,
                Coinbase = block.Miner,
                Difficulty = block.Difficulty != null
                    ? EvmUInt256BigIntegerExtensions.FromBigInteger(block.Difficulty.Value).ToBigEndian()
                    : new byte[32],
                ParentHash = block.ParentHash?.HexToByteArray() ?? new byte[32],
                ExtraData = block.ExtraData?.HexToByteArray() ?? new byte[0],
                MixHash = block.MixHash?.HexToByteArray() ?? new byte[32],
                Nonce = !string.IsNullOrEmpty(block.Nonce) ? block.Nonce.HexToByteArray() : new byte[8],
                ComputePostStateRoot = true,
                Features = new BlockFeatureConfig
                {
                    Fork = Nethereum.EVM.MainnetHardforkActivations.ResolveAt(
                        (long)block.Number.Value, (ulong)block.Timestamp.Value)
                },
                Transactions = new List<BlockWitnessTransaction>()
            };

            long pathAGasSum = 0;

            for (int i = 0; i < maxTx; i++)
            {
                var rpcTx = block.Transactions[i];
                _output.WriteLine($"\n--- TX {i}: hash={rpcTx.TransactionHash}");
                _output.WriteLine($"    from={rpcTx.From} to={rpcTx.To ?? "CREATE"} value={rpcTx.Value.Value}");

                byte[] rlpEncoded;
                List<string> authorities;
                try
                {
                    var signed = rpcTx.ToSignedTransaction(chainId).SignedTransaction;
                    rlpEncoded = signed.GetRLPEncoded();
                    authorities = signed.RecoverAuthorities();
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"    SKIP (RLP encode failed): {ex.Message}");
                    continue;
                }

                var wtx = new BlockWitnessTransaction
                {
                    From = rpcTx.From,
                    RlpEncoded = rlpEncoded,
                    AuthorisationAuthorities = authorities
                };

                try
                {
                    var ctx = TransactionContextFactory.FromBlockWitnessTransaction(wtx, blockData, executionState);
                    var result = await executor.ExecuteAsync(ctx);

                    _output.WriteLine($"    A: success={result.Success} gasUsed={result.GasUsed} error={result.Error}");

                    if (result.Success || !result.IsValidationError)
                    {
                        pathAGasSum += result.GasUsed;
                        blockData.Transactions.Add(wtx);
                    }
                    else
                    {
                        _output.WriteLine($"    SKIP tx from witness — validation error");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"    SKIP (execute threw): {ex.Message}");
                }
            }

            Assert.True(blockData.Transactions.Count > 0, "No transactions completed path A — cannot generate witness");

            blockData.Accounts = recorder.GetWitnessAccounts();
            _output.WriteLine($"\nRecorded witness: {blockData.Accounts.Count} accounts, {blockData.Transactions.Count} txs");
            foreach (var acc in blockData.Accounts)
            {
                int storageSlots = acc.Storage?.Count ?? 0;
                int codeLen = acc.Code?.Length ?? 0;
                _output.WriteLine($"  {acc.Address}: balance={acc.Balance}, nonce={acc.Nonce}, code={codeLen}b, storage={storageSlots} slots");
            }

            var witnessBytes = BinaryBlockWitness.Serialize(blockData);
            _output.WriteLine($"\nSerialized witness: {witnessBytes.Length} bytes (v{witnessBytes[0]})");

            Assert.True(blockData.Accounts.Count > 0, "No accounts in witness");
            Assert.True(witnessBytes.Length > 100, "Witness too small");
            Assert.Equal(BinaryBlockWitness.VERSION, witnessBytes[0]);

            var roundTrip = BinaryBlockWitness.Deserialize(witnessBytes);
            Assert.Equal(blockData.Accounts.Count, roundTrip.Accounts.Count);
            Assert.Equal(blockData.Transactions.Count, roundTrip.Transactions.Count);

            var outputDir = Path.Combine(ZiskEmulatorRunner.FindProjectRoot(Directory.GetCurrentDirectory())
                ?? Directory.GetCurrentDirectory(), "scripts", "zisk-output", "witnesses");
            var witnessFile = ZiskEmulatorRunner.WriteLegacyInput(outputDir, $"mainnet_block_{block.Number.Value}", witnessBytes);
            _output.WriteLine($"Witness file: {witnessFile}");

            _output.WriteLine($"\nPath A cumulative gas (local): {pathAGasSum}");

            var elfPath = ZiskEmulatorRunner.FindDefaultElfPath();
            Assert.True(elfPath != null,
                "Zisk guest ELF not found at scripts/zisk-output/nethereum_evm_elf, so the "
                + "guest/host post-state-root parity gate cannot run. Build it with "
                + "`bash scripts/build-evm-zisk.sh`. This is a hard failure by design: a "
                + "parity comparison that is skipped must never report success.");

            _output.WriteLine($"ELF: {elfPath}");
            var ziskResult = ZiskEmulatorRunner.RunZiskEmu(elfPath, witnessFile, timeoutMs: 180000, maxSteps: 500000000);

            _output.WriteLine("=== ziskemu output ===");
            _output.WriteLine(ziskResult.RawOutput ?? "<no output>");
            _output.WriteLine("======================");

            Assert.True(ziskResult.Success,
                $"ziskemu did not return BIN:OK — error: {ziskResult.Error}. This test serialises "
                + $"BinaryBlockWitness v{BinaryBlockWitness.VERSION}; a version complaint means the ELF at "
                + $"{elfPath} was built against an older one and needs rebuilding "
                + $"(`bash scripts/build-evm-zisk.sh`), not that the guest engine diverged. "
                + $"Raw output:\n{ziskResult.RawOutput}");
            Assert.True(ziskResult.GasUsed > 0, "ziskemu reported zero gas");
            Assert.False(string.IsNullOrEmpty(ziskResult.StateRootHex), "ziskemu did not report state_root");

            _output.WriteLine($"\nPath B (ziskemu): gas={ziskResult.GasUsed} state_root={ziskResult.StateRootHex} block_hash={ziskResult.BlockHashHex}");

            var gasDelta = pathAGasSum - ziskResult.GasUsed;
            _output.WriteLine($"Gas delta (pathA - ziskemu) = {gasDelta}");
            _output.WriteLine($"Ziskemu state root: {ziskResult.StateRootHex}");

            blockData.ComputePostStateRoot = true;
            var registry = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance;
            var blockExecutorResult = await Nethereum.EVM.Execution.BlockExecutor.ExecuteAsync(
                blockData,
                RlpBlockEncodingProvider.Instance,
                registry,
                new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance));

            var blockExecutorStateRootHex = blockExecutorResult.StateRoot == null
                ? "<null>"
                : "0x" + blockExecutorResult.StateRoot.ToHex();
            _output.WriteLine($"\nPath B (async BlockExecutor): gas={blockExecutorResult.CumulativeGasUsed} state_root={blockExecutorStateRootHex}");

            var asyncGasDelta = ziskResult.GasUsed - blockExecutorResult.CumulativeGasUsed;
            _output.WriteLine($"ziskemu vs async BlockExecutor: gas delta={asyncGasDelta}");

            AssertUsableStateRoot("ziskemu (RISC-V guest)", ziskResult.StateRootHex, block.Number.Value);
            AssertUsableStateRoot(".NET async BlockExecutor",
                blockExecutorResult.StateRoot == null ? null : blockExecutorStateRootHex,
                block.Number.Value);

            Assert.True(
                string.Equals(ziskResult.StateRootHex, blockExecutorStateRootHex,
                    System.StringComparison.OrdinalIgnoreCase),
                $"POST-STATE ROOT DIVERGENCE at block {block.Number.Value} "
                + $"(fork={blockData.Features.Fork}, txs in witness={blockData.Transactions.Count}, "
                + $"accounts={blockData.Accounts.Count}).\n"
                + $"  expected (ziskemu / RISC-V guest, {elfPath}):\n"
                + $"      {ziskResult.StateRootHex}\n"
                + $"  actual   (.NET async EVM.Execution.BlockExecutor):\n"
                + $"      {blockExecutorStateRootHex}\n"
                + $"  gas: ziskemu={ziskResult.GasUsed} async={blockExecutorResult.CumulativeGasUsed} "
                + $"delta={asyncGasDelta}\n"
                + $"  witness: {witnessFile}");

            parityGateRan = true;

            Assert.True(asyncGasDelta == 0,
                $"CUMULATIVE GAS DIVERGENCE at block {block.Number.Value} "
                + $"(post-state roots agreed at {blockExecutorStateRootHex}).\n"
                + $"  expected (ziskemu / RISC-V guest): {ziskResult.GasUsed}\n"
                + $"  actual   (.NET async BlockExecutor): {blockExecutorResult.CumulativeGasUsed}\n"
                + $"  delta (ziskemu - async) = {asyncGasDelta}\n"
                + $"  witness: {witnessFile}");

            if (Environment.GetEnvironmentVariable("ZISK_PROVE_E2E") == "1")
            {
                _output.WriteLine("\nRunning cargo-zisk prove (ZISK_PROVE_E2E=1)...");
                var proveResult = ZiskEmulatorRunner.RunCargoZiskProve(elfPath, witnessFile);
                _output.WriteLine(proveResult.RawOutput ?? "<no output>");
                Assert.True(proveResult.Success,
                    $"cargo-zisk prove failed (exit {proveResult.ExitCode}): {proveResult.Error}");
                _output.WriteLine("Proof generated successfully");
            }

            Assert.True(parityGateRan,
                "Test reached the end without executing the post-state-root parity "
                + "comparison. A green result here would be meaningless.");

            _output.WriteLine("\nSUCCESS — witness generated from mainnet RPC and validated via ziskemu");
        }
    }
}
