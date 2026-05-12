using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class GenerateTestWitness
    {
        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string CONTRACT = "0x1000000000000000000000000000000000000000";

        [Fact]
        public void GenerateSimpleSstoreWitness()
        {
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
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
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                }
            };

            var bytes = BinaryBlockWitness.Serialize(block);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(GenerateTestWitness).Assembly.Location),
                "..", "..", "..", "..", "..", "zisk", "output", "test_sstore.bin");
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, bytes);

            Assert.True(bytes.Length > 0);
            Assert.Equal(1, bytes[0]);

            var deserialized = BinaryBlockWitness.Deserialize(bytes);
            Assert.Equal(block.Transactions[0].From, deserialized.Transactions[0].From);
            Assert.Equal(block.Accounts.Count, deserialized.Accounts.Count);
        }

        [Theory]
        [InlineData("eth_transfer_noroot", false, false, false)]
        [InlineData("eth_transfer_patricia", true, true, false)]
        [InlineData("sstore_noroot", false, false, true)]
        [InlineData("sstore_patricia", true, true, true)]
        public void GenerateZiskWitness(string name, bool computeRoot, bool commitments, bool useSstore)
        {
            var receiver = "0x1111111111111111111111111111111111111111";
            var contractCode = useSstore ? new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 } : null;

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = commitments,
                ComputePostStateRoot = computeRoot,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    useSstore
                        ? TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                        : TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(1000), 0, 10, 21000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    useSstore
                        ? new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                        : new WitnessAccount { Address = receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };

            var bytes = BinaryBlockWitness.Serialize(block);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(GenerateTestWitness).Assembly.Location),
                "..", "..", "..", "..", "..", "zisk", "output", $"witness_{name}.bin");
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, bytes);

            var standardPath = Path.ChangeExtension(outputPath, null) + "_standard.bin";
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((ulong)(16 + bytes.Length));
                bw.Write((ulong)0);
                bw.Write((ulong)bytes.Length);
                bw.Write(bytes);
                File.WriteAllBytes(standardPath, ms.ToArray());
            }

            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void GenerateMultiTxBlockWitness()
        {
            var receiver1 = "0x1111111111111111111111111111111111111111";
            var receiver2 = "0x2222222222222222222222222222222222222222";
            var receiver3 = "0x3333333333333333333333333333333333333333";

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(receiver1, new EvmUInt256(1000), 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(receiver2, new EvmUInt256(2000), 1, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(receiver3, new EvmUInt256(3000), 2, 10, 21000, SENDER_KEY),
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver1, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver2, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver3, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "multi_tx_3transfers");
        }

        [Fact]
        public void GenerateContractCreationWitness()
        {
            // Simple contract: PUSH1 0x42 PUSH1 0x00 SSTORE STOP — returned as init code
            // PUSH1 6 PUSH1 0x0C PUSH1 0x00 CODECOPY PUSH1 6 PUSH1 0x00 RETURN | PUSH1 0x42 PUSH1 0x00 SSTORE STOP
            var initCode = new byte[] {
                0x60, 0x06, 0x60, 0x0C, 0x60, 0x00, 0x39, // CODECOPY(0, 12, 6)
                0x60, 0x06, 0x60, 0x00, 0xF3,              // RETURN(0, 6)
                0x60, 0x42, 0x60, 0x00, 0x55, 0x00          // runtime: PUSH 0x42, PUSH 0x00, SSTORE, STOP
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall("", initCode, EvmUInt256.Zero, 0, 10, 200000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "contract_creation");
        }

        [Fact]
        public void GenerateSha256PrecompileWitness()
        {
            // Contract that calls SHA256 precompile (0x02) with 32 bytes of input
            // PUSH32 <data> PUSH1 0x00 MSTORE        — store 32 bytes at memory[0]
            // PUSH1 0x20 PUSH1 0x20 PUSH1 0x20 PUSH1 0x00 PUSH1 0x00 PUSH20 0x02 PUSH2 0xFFFF STATICCALL
            // PUSH1 0x20 PUSH1 0x20 RETURN
            var sha256Contract = new byte[] {
                // Store input: PUSH32 0x48656c6c6f... PUSH1 0 MSTORE
                0x7F, // PUSH32
                0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, // "Hello Wo"
                0x72, 0x6C, 0x64, 0x21, 0x00, 0x00, 0x00, 0x00, // "rld!...."
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x60, 0x00, 0x52, // MSTORE at 0
                // STATICCALL(gas, 0x02, inOffset=0, inSize=32, retOffset=32, retSize=32)
                0x60, 0x20, // retSize
                0x60, 0x20, // retOffset
                0x60, 0x20, // inSize
                0x60, 0x00, // inOffset
                0x60, 0x02, // address (SHA256 precompile)
                0x5A,       // GAS
                0xFA,       // STATICCALL
                0x50,       // POP result
                0x60, 0x20, // size
                0x60, 0x20, // offset
                0xF3,       // RETURN
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 200000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = sha256Contract, Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "sha256_precompile");
        }

        [Fact]
        public void GenerateModexpPrecompileWitness()
        {
            // Contract that calls MODEXP precompile (0x05)
            // modexp(base=3, exp=5, mod=13) = 3^5 mod 13 = 243 mod 13 = 9
            var modexpContract = new byte[] {
                // Store MODEXP input at memory[0]: base_len(32) + exp_len(32) + mod_len(32) + base + exp + mod
                // base_len = 1
                0x60, 0x01, 0x60, 0x00, 0x52,
                // exp_len = 1
                0x60, 0x01, 0x60, 0x20, 0x52,
                // mod_len = 1
                0x60, 0x01, 0x60, 0x40, 0x52,
                // base = 3
                0x60, 0x03, 0x60, 0x60, 0x52,
                // exp = 5
                0x60, 0x05, 0x60, 0x80, 0x52,
                // mod = 13
                0x60, 0x0D, 0x60, 0xA0, 0x52,
                // STATICCALL(gas, 0x05, inOffset=0, inSize=0xC0, retOffset=0xC0, retSize=0x20)
                0x60, 0x20, // retSize
                0x61, 0x00, 0xC0, // retOffset
                0x61, 0x00, 0xC0, // inSize
                0x60, 0x00, // inOffset
                0x60, 0x05, // address (MODEXP precompile)
                0x5A,       // GAS
                0xFA,       // STATICCALL
                0x50,       // POP result
                0x60, 0x20, // size
                0x61, 0x00, 0xC0, // offset
                0xF3,       // RETURN
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 200000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = modexpContract, Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "modexp_precompile");
        }

        [Fact]
        public void GenerateMultiTxMixedBlockWitness()
        {
            var receiver = "0x1111111111111111111111111111111111111111";
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 }; // PUSH 0x42, PUSH 0, SSTORE, STOP

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(1000), 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 1, 10, 100000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(2000), 2, 10, 21000, SENDER_KEY),
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "multi_tx_mixed");
        }

        private void WriteWitness(BlockWitnessData block, string name)
        {
            var bytes = BinaryBlockWitness.Serialize(block);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(GenerateTestWitness).Assembly.Location),
                "..", "..", "..", "..", "..", "zisk", "output", $"witness_{name}.bin");
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, bytes);

            var standardPath = Path.ChangeExtension(outputPath, null) + "_standard.bin";
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((ulong)(16 + bytes.Length));
                bw.Write((ulong)0);
                bw.Write((ulong)bytes.Length);
                bw.Write(bytes);
                File.WriteAllBytes(standardPath, ms.ToArray());
            }

            Assert.True(bytes.Length > 0);
            Assert.Equal(1, bytes[0]);
        }

        [Theory]
        [InlineData("blake3", WitnessHashFunction.Blake3)]
        [InlineData("poseidon", WitnessHashFunction.Poseidon)]
        public void GenerateBinaryTrieWitness(string name, WitnessHashFunction hashFunction)
        {
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var features = new BlockFeatureConfig
            {
                Fork = HardforkName.Osaka,
                MaxBlobsPerBlock = 9,
                StateTree = WitnessStateTreeType.Binary,
                HashFunction = hashFunction
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = features,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                }
            };

            var bytes = BinaryBlockWitness.Serialize(block);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(GenerateTestWitness).Assembly.Location),
                "..", "..", "..", "..", "..", "zisk", "output", $"test_sstore_binary_{name}.bin");
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, bytes);

            Assert.True(bytes.Length > 0);
            Assert.Equal(1, bytes[0]);

            var deserialized = BinaryBlockWitness.Deserialize(bytes);
            Assert.Equal(WitnessStateTreeType.Binary, deserialized.Features.StateTree);
            Assert.Equal(hashFunction, deserialized.Features.HashFunction);
        }

        [Fact]
        public void GenerateEcrecoverPrecompileWitness()
        {
            var key = new Nethereum.Signer.EthECKey(SENDER_KEY);
            var hash = Sha3Keccack.Current.CalculateHash(
                System.Text.Encoding.UTF8.GetBytes("test message"));
            var sig = key.SignAndCalculateV(hash);
            var v = sig.V[0];
            var r = sig.R;
            var s = sig.S;

            var code = new List<byte>();

            // Store hash at mem[0x00]
            code.Add(0x7F); code.AddRange(hash);
            code.Add(0x60); code.Add(0x00); code.Add(0x52);

            // Store v at mem[0x20]
            code.Add(0x60); code.Add(v);
            code.Add(0x60); code.Add(0x20); code.Add(0x52);

            // Store r at mem[0x40]
            code.Add(0x7F);
            var rPadded = new byte[32];
            Array.Copy(r, 0, rPadded, 32 - r.Length, r.Length);
            code.AddRange(rPadded);
            code.Add(0x60); code.Add(0x40); code.Add(0x52);

            // Store s at mem[0x60]
            code.Add(0x7F);
            var sPadded = new byte[32];
            Array.Copy(s, 0, sPadded, 32 - s.Length, s.Length);
            code.AddRange(sPadded);
            code.Add(0x60); code.Add(0x60); code.Add(0x52);

            // STATICCALL(gas, 0x01, 0, 128, 128, 32)
            code.Add(0x60); code.Add(0x20);
            code.Add(0x60); code.Add(0x80);
            code.Add(0x60); code.Add(0x80);
            code.Add(0x60); code.Add(0x00);
            code.Add(0x60); code.Add(0x01);
            code.Add(0x5A); code.Add(0xFA); code.Add(0x50);

            // MLOAD result, SSTORE to slot 0
            code.Add(0x60); code.Add(0x80); code.Add(0x51);
            code.Add(0x60); code.Add(0x00); code.Add(0x55);

            // RETURN
            code.Add(0x60); code.Add(0x20);
            code.Add(0x60); code.Add(0x80);
            code.Add(0xF3);

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 200000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = code.ToArray(), Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "ecrecover_precompile");
        }

        [Fact]
        public void GenerateBn128AddMulPrecompileWitness()
        {
            var g1x = new byte[32]; g1x[31] = 1;
            var g1y = new byte[32]; g1y[31] = 2;

            var code = new List<byte>();

            // ecAdd(G1, G1) → result at mem[0x80..0xBF]
            code.Add(0x7F); code.AddRange(g1x); code.Add(0x60); code.Add(0x00); code.Add(0x52);
            code.Add(0x7F); code.AddRange(g1y); code.Add(0x60); code.Add(0x20); code.Add(0x52);
            code.Add(0x7F); code.AddRange(g1x); code.Add(0x60); code.Add(0x40); code.Add(0x52);
            code.Add(0x7F); code.AddRange(g1y); code.Add(0x60); code.Add(0x60); code.Add(0x52);

            // STATICCALL(gas, 0x06, 0, 128, 0x80, 64)
            code.Add(0x60); code.Add(0x40);
            code.Add(0x60); code.Add(0x80);
            code.Add(0x60); code.Add(0x80);
            code.Add(0x60); code.Add(0x00);
            code.Add(0x60); code.Add(0x06);
            code.Add(0x5A); code.Add(0xFA); code.Add(0x50);

            // ecMul(G1, 2) → result at mem[0xC0..0xFF]
            code.Add(0x7F); code.AddRange(g1x); code.Add(0x60); code.Add(0x00); code.Add(0x52);
            code.Add(0x7F); code.AddRange(g1y); code.Add(0x60); code.Add(0x20); code.Add(0x52);
            var scalar2 = new byte[32]; scalar2[31] = 2;
            code.Add(0x7F); code.AddRange(scalar2); code.Add(0x60); code.Add(0x40); code.Add(0x52);

            // STATICCALL(gas, 0x07, 0, 96, 0xC0, 64)
            code.Add(0x60); code.Add(0x40);
            code.Add(0x60); code.Add(0xC0);
            code.Add(0x60); code.Add(0x60);
            code.Add(0x60); code.Add(0x00);
            code.Add(0x60); code.Add(0x07);
            code.Add(0x5A); code.Add(0xFA); code.Add(0x50);

            // Compare add vs mul results, SSTORE match flag to slot 0
            code.Add(0x60); code.Add(0x80); code.Add(0x51); // MLOAD 0x80
            code.Add(0x60); code.Add(0xC0); code.Add(0x51); // MLOAD 0xC0
            code.Add(0x14);                                   // EQ
            code.Add(0x60); code.Add(0xA0); code.Add(0x51); // MLOAD 0xA0
            code.Add(0x60); code.Add(0xE0); code.Add(0x51); // MLOAD 0xE0
            code.Add(0x14);                                   // EQ
            code.Add(0x16);                                   // AND
            code.Add(0x60); code.Add(0x00); code.Add(0x55); // SSTORE slot 0
            code.Add(0x00);                                   // STOP

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 200000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = code.ToArray(), Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "bn128_add_mul");
        }

        [Fact]
        public void GenerateBn128PairingPrecompileWitness()
        {
            var g1x = new byte[32]; g1x[31] = 1;
            var g1y = new byte[32]; g1y[31] = 2;
            var negG1y = "30644e72e131a029b85045b68181585d97816a916871ca8d3c208c16d87cfd45".HexToByteArray();

            var g2xi = "198e9393920d483a7260bfb731fb5d25f1aa493335a9e71297e485b7aef312c2".HexToByteArray();
            var g2xr = "1800deef121f1e76426a00665e5c4479674322d4f75edadd46debd5cd992f6ed".HexToByteArray();
            var g2yi = "090689d0585ff075ec9e99ad690c3395bc4b313370b38ef355acddb9e557b7367".HexToByteArray();
            var g2yr = "12c85ea5db8c6deb4aab71808dcb408fe3d1e7690c43d37b4ce6cc0166fa7daa".HexToByteArray();

            var code = new List<byte>();
            void Push32(byte[] val)
            {
                var padded = new byte[32];
                if (val.Length <= 32) Array.Copy(val, 0, padded, 32 - val.Length, val.Length);
                else Array.Copy(val, val.Length - 32, padded, 0, 32);
                code.Add(0x7F); code.AddRange(padded);
            }
            void Mstore(int off)
            {
                if (off < 256) { code.Add(0x60); code.Add((byte)off); }
                else { code.Add(0x61); code.Add((byte)(off >> 8)); code.Add((byte)(off & 0xFF)); }
                code.Add(0x52);
            }

            // Pair 1: G1 + G2
            Push32(g1x); Mstore(0x00);
            Push32(g1y); Mstore(0x20);
            Push32(g2xi); Mstore(0x40);
            Push32(g2xr); Mstore(0x60);
            Push32(g2yi); Mstore(0x80);
            Push32(g2yr); Mstore(0xA0);

            // Pair 2: -G1 + G2
            Push32(g1x); Mstore(0xC0);
            Push32(negG1y); Mstore(0xE0);
            Push32(g2xi); Mstore(0x100);
            Push32(g2xr); Mstore(0x120);
            Push32(g2yi); Mstore(0x140);
            Push32(g2yr); Mstore(0x160);

            // STATICCALL(gas, 0x08, 0, 384, 0x180, 32)
            code.Add(0x60); code.Add(0x20);
            code.Add(0x61); code.Add(0x01); code.Add(0x80);
            code.Add(0x61); code.Add(0x01); code.Add(0x80);
            code.Add(0x60); code.Add(0x00);
            code.Add(0x60); code.Add(0x08);
            code.Add(0x5A); code.Add(0xFA); code.Add(0x50);

            // MLOAD result, SSTORE to slot 0
            code.Add(0x61); code.Add(0x01); code.Add(0x80); code.Add(0x51);
            code.Add(0x60); code.Add(0x00); code.Add(0x55);
            code.Add(0x00);

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 500000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = code.ToArray(), Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "bn128_pairing");
        }

        [Fact]
        public void GenerateSstoreBinaryPoseidonWitness()
        {
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var features = new BlockFeatureConfig
            {
                Fork = HardforkName.Osaka,
                MaxBlobsPerBlock = 9,
                StateTree = WitnessStateTreeType.Binary,
                HashFunction = WitnessHashFunction.Poseidon
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = features,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                }
            };

            WriteWitness(block, "sstore_binary_poseidon");
        }

        [Fact]
        public void GenerateMultiTxBinaryPoseidonWitness()
        {
            var receiver = "0x1111111111111111111111111111111111111111";
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var features = new BlockFeatureConfig
            {
                Fork = HardforkName.Osaka,
                MaxBlobsPerBlock = 9,
                StateTree = WitnessStateTreeType.Binary,
                HashFunction = WitnessHashFunction.Poseidon
            };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = features,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(1000), 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 1, 10, 100000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(2000), 2, 10, 21000, SENDER_KEY),
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() },
                }
            };

            WriteWitness(block, "multi_tx_binary_poseidon");
        }
    }
}
