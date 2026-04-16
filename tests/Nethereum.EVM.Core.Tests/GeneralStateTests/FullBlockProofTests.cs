using System;
using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Core.Tests;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class FullBlockProofTests
    {
        private readonly ITestOutputHelper _output;
        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string RECEIVER = "0x1000000000000000000000000000000000000001";
        private const string CONTRACT = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string COINBASE = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba";

        public FullBlockProofTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void FullBlockProof_TwoTransfers_ComputeAllRootsAndHash()
        {
            var block = CreateSignedBlockProof();
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.NotNull(result.StateRoot);
            Assert.NotNull(result.TransactionsRoot);
            Assert.NotNull(result.ReceiptsRoot);
            Assert.NotNull(result.BlockHash);
            Assert.True(result.TxResults[0].Success);
            Assert.True(result.TxResults[1].Success);

            _output.WriteLine($"State root:  0x{result.StateRoot.ToHex()}");
            _output.WriteLine($"Tx root:     0x{result.TransactionsRoot.ToHex()}");
            _output.WriteLine($"Receipt root:0x{result.ReceiptsRoot.ToHex()}");
            _output.WriteLine($"Block hash:  0x{result.BlockHash.ToHex()}");
            _output.WriteLine($"Gas:         {result.CumulativeGasUsed}");
        }

        [Fact]
        public void FullBlockProof_WitnessRoundtrip_SameRoots()
        {
            var block = CreateSignedBlockProof();
            var direct = BlockExecutionHelper.ExecuteBlock(block);

            var bytes = BinaryBlockWitness.Serialize(block);
            var deserialized = BinaryBlockWitness.Deserialize(bytes);
            var witness = BlockExecutionHelper.ExecuteBlock(deserialized);

            Assert.True(direct.StateRoot.AreTheSame(witness.StateRoot),
                $"State root: direct=0x{direct.StateRoot.ToHex()} witness=0x{witness.StateRoot.ToHex()}");
            Assert.True(direct.BlockHash.AreTheSame(witness.BlockHash),
                $"Block hash: direct=0x{direct.BlockHash.ToHex()} witness=0x{witness.BlockHash.ToHex()}");
        }

        [Fact]
        public void FullBlockProof_TransferWithValue_StateRootReflectsBalances()
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = COINBASE,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, new EvmUInt256(1000000), 0, 10, 21000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(10000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = RECEIVER, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };

            var result = BlockExecutionHelper.ExecuteBlock(block);
            Assert.True(result.TxResults[0].Success, $"Failed: {result.TxResults[0].Error}");

            var receiverState = result.FinalExecutionState.CreateOrGetAccountExecutionState(RECEIVER);
            Assert.Equal(new EvmUInt256(1000000), receiverState.Balance.GetTotalBalance());
        }

        [Fact]
        public void FullBlockProof_ContractCallWithLog_ReceiptsRootIncludesLogs()
        {
            // Contract: LOG0(mem[0:32])
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xA0, 0x00 };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = COINBASE,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(10000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                }
            };

            var result = BlockExecutionHelper.ExecuteBlock(block);
            Assert.True(result.TxResults[0].Success, $"Failed: {result.TxResults[0].Error}");
            Assert.True(result.Receipts[0].Logs.Count > 0, "Should have logs");
            Assert.NotNull(result.ReceiptsRoot);
        }

        [Fact]
        public void FullBlockProof_DeterministicBlockHash()
        {
            var block = CreateSignedBlockProof();
            var r1 = BlockExecutionHelper.ExecuteBlock(block);
            var r2 = BlockExecutionHelper.ExecuteBlock(block);

            Assert.True(r1.BlockHash.AreTheSame(r2.BlockHash), "Same block must produce same hash");
            Assert.True(r1.StateRoot.AreTheSame(r2.StateRoot));
        }

        [Fact]
        public void FullBlockProof_ZiskEmu_TwoTransfers()
        {
            var elfPath = FindElfPath();
            if (string.IsNullOrEmpty(elfPath))
            {
                _output.WriteLine("SKIP — ELF not found");
                return;
            }

            var block = CreateSignedBlockProof();
            var witnessBytes = BinaryBlockWitness.Serialize(block);

            int padLen = (8 - witnessBytes.Length % 8) % 8;
            if (padLen > 0)
            {
                var padded = new byte[witnessBytes.Length + padLen];
                Array.Copy(witnessBytes, padded, witnessBytes.Length);
                witnessBytes = padded;
            }

            var legacyInput = new byte[16 + witnessBytes.Length];
            BitConverter.GetBytes((long)0).CopyTo(legacyInput, 0);
            BitConverter.GetBytes((long)witnessBytes.Length).CopyTo(legacyInput, 8);
            Array.Copy(witnessBytes, 0, legacyInput, 16, witnessBytes.Length);

            var witnessFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zisk-block-test.bin");
            System.IO.File.WriteAllBytes(witnessFile, legacyInput);

            var wsl = witnessFile.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");
            var elf = elfPath.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"-d Ubuntu -e bash -c \"export PATH=$HOME/.zisk/bin:$HOME/.cargo/bin:$PATH && ziskemu -e {elf} --legacy-inputs {wsl} -n 500000000\"",
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(120000);

            foreach (var line in output.Split('\n'))
                if (line.Trim().StartsWith("BIN:")) _output.WriteLine(line.Trim());

            Assert.Contains("BIN:OK gas=", output);

            var syncResult = BlockExecutionHelper.ExecuteBlock(block);
            var expectedHash = "0x" + syncResult.BlockHash.ToHex();

            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("BIN:block_hash="))
                    Assert.Equal(expectedHash, t.Substring("BIN:block_hash=".Length));
            }
        }

        private static BlockWitnessData CreateSignedBlockProof()
        {
            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = COINBASE,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, EvmUInt256.Zero, 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, EvmUInt256.Zero, 1, 10, 21000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = RECEIVER, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };
        }

        private static string FindElfPath()
        {
            var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var p = System.IO.Path.Combine(dir.FullName, "zisk", "output", "nethereum_evm_elf");
                    return System.IO.File.Exists(p) ? p : null;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
