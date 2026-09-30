using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class SimulateV1BlockSealingTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Target = "0x9999999999999999999999999999999999999a";
        private const string TargetTwo = "0x9999999999999999999999999999999999999b";

        private const string SstoreSlot7Equals42 = "0x602a60075500";

        private const string SstoreSlot7Equals42ThenRevert = "0x602a60075560006000fd";

        private const string WriteWhenValueSentElseReadSlot7 =
            "0x3415600a57602a6007555b60075460005260206000f3";

        private static async Task<DevChainNode> StartAsync(string fork = "prague")
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = fork });
            await node.StartAsync(new[] { Caller });
            return node;
        }

        private static EthSimulateInput OneBlockOneCall(string to, string data, HexBigInteger value = null) => new EthSimulateInput
        {
            BlockStateCalls = new List<BlockStateCall>
            {
                new BlockStateCall
                {
                    Calls = new List<TransactionInput> { new TransactionInput { From = Caller, To = to, Data = data, Value = value } }
                }
            }
        };

        [Fact]
        public async Task Given_SimulatedCallWritesStorage_When_SimulateV1Requested_Then_StateRootDiffersFromBaseBlock_AndRevertedCallLeavesItUnchanged()
        {
            using var writeNode = await StartAsync();
            await writeNode.SetCodeAsync(Target, SstoreSlot7Equals42.HexToByteArray());
            var writeResult = await writeNode.SimulateAsync(OneBlockOneCall(Target, "0x"), null);

            using var revertNode = await StartAsync();
            await revertNode.SetCodeAsync(Target, SstoreSlot7Equals42ThenRevert.HexToByteArray());
            var revertResult = await revertNode.SimulateAsync(OneBlockOneCall(Target, "0x"), null);

            Assert.NotNull(writeResult[0].StateRoot);
            Assert.NotEmpty(writeResult[0].StateRoot);
            Assert.NotNull(writeResult[0].ReceiptsRoot);
            Assert.NotNull(writeResult[0].BlockHash);
            Assert.NotNull(writeResult[0].TransactionsRoot);
            Assert.NotNull(writeResult[0].LogsBloom);

            Assert.Equal("0x1", writeResult[0].Calls[0].Status);
            Assert.Equal("0x0", revertResult[0].Calls[0].Status);

            Assert.NotEqual(writeResult[0].StateRoot, revertResult[0].StateRoot);

            using var writeNodeAgain = await StartAsync();
            await writeNodeAgain.SetCodeAsync(Target, SstoreSlot7Equals42.HexToByteArray());
            var writeResultAgain = await writeNodeAgain.SimulateAsync(OneBlockOneCall(Target, "0x"), null);

            Assert.Equal(writeResult[0].StateRoot, writeResultAgain[0].StateRoot);
            Assert.Equal(writeResult[0].ReceiptsRoot, writeResultAgain[0].ReceiptsRoot);
            Assert.Equal(writeResult[0].TransactionsRoot, writeResultAgain[0].TransactionsRoot);
            Assert.Equal(writeResult[0].LogsBloom, writeResultAgain[0].LogsBloom);
            Assert.Equal(writeResult[0].BlockHash, writeResultAgain[0].BlockHash);
        }

        [Fact]
        public async Task Given_SimulateV1WroteToAnAddress_When_EthGetStorageAtIsCalledAfterwards_Then_ReturnsThePreSimulateValue()
        {
            using var node = await StartAsync();
            await node.SetCodeAsync(Target, SstoreSlot7Equals42.HexToByteArray());

            var before = await node.GetStorageAtAsync(Target, new EvmUInt256(7L));
            Assert.True(before == null || before.All(b => b == 0));

            var result = await node.SimulateAsync(OneBlockOneCall(Target, "0x"), null);
            Assert.Equal("0x1", result[0].Calls[0].Status);
            Assert.NotNull(result[0].StateRoot);

            var after = await node.GetStorageAtAsync(Target, new EvmUInt256(7L));
            Assert.True(after == null || after.All(b => b == 0));
        }

        [Fact]
        public async Task Given_TwoChainedBlockStateCalls_When_SimulateV1Requested_Then_SecondBlockHeaderChainsFromFirstsStampedRoot_AndSeesFirstBlocksWrite()
        {
            using var node = await StartAsync();
            await node.SetCodeAsync(Target, WriteWhenValueSentElseReadSlot7.HexToByteArray());

            var input = new EthSimulateInput
            {
                BlockStateCalls = new List<BlockStateCall>
                {
                    new BlockStateCall
                    {
                        Calls = new List<TransactionInput>
                        {
                            new TransactionInput { From = Caller, To = Target, Data = "0x", Value = new HexBigInteger(1) }
                        }
                    },
                    new BlockStateCall
                    {
                        Calls = new List<TransactionInput>
                        {
                            new TransactionInput { From = Caller, To = Target, Data = "0x" }
                        }
                    }
                }
            };

            var results = await node.SimulateAsync(input, null);

            Assert.Equal(2, results.Count);
            Assert.Equal(results[0].BlockHash, results[1].ParentHash);

            Assert.Equal(42, ReturnedUInt256(results[0].Calls[0]));
            Assert.Equal(42, ReturnedUInt256(results[1].Calls[0]));
        }

        private static BigInteger ReturnedUInt256(EthSimulateCallResult call)
        {
            var bytes = call.ReturnData.HexToByteArray();
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }

        [Fact]
        public async Task Given_TwoCallsEachEmittingDifferentTopics_When_SimulateV1Requested_Then_LogsBloomIsTheOrOfBothCallsBlooms_MatchingBlockProducersCombineBloom()
        {
            using var node = await StartAsync();

            const string topicOne = "0x1111111111111111111111111111111111111111111111111111111111111111";
            const string topicTwo = "0x2222222222222222222222222222222222222222222222222222222222222222";
            var topicOne32 = topicOne.RemoveHexPrefix().Substring(0, 64);
            var topicTwo32 = topicTwo.RemoveHexPrefix().Substring(0, 64);

            await node.SetCodeAsync(Target, EmitLog1(topicOne32).HexToByteArray());
            await node.SetCodeAsync(TargetTwo, EmitLog1(topicTwo32).HexToByteArray());

            var input = new EthSimulateInput
            {
                BlockStateCalls = new List<BlockStateCall>
                {
                    new BlockStateCall
                    {
                        Calls = new List<TransactionInput>
                        {
                            new TransactionInput { From = Caller, To = Target, Data = "0x" },
                            new TransactionInput { From = Caller, To = TargetTwo, Data = "0x" }
                        }
                    }
                }
            };

            var results = await node.SimulateAsync(input, null);
            var block = results[0];

            Assert.NotNull(block.LogsBloom);
            Assert.Single(block.Calls[0].Logs);
            Assert.Single(block.Calls[1].Logs);

            var expectedBloom = new byte[256];
            IndependentlyAddToBloom(expectedBloom, Target);
            IndependentlyAddToBloom(expectedBloom, "0x" + topicOne32);
            IndependentlyAddToBloom(expectedBloom, TargetTwo);
            IndependentlyAddToBloom(expectedBloom, "0x" + topicTwo32);

            Assert.Equal(expectedBloom.ToHex(true), block.LogsBloom);
        }

        private static string EmitLog1(string topic32BytesHexNoPrefix)
        {
            return "0x7f" + topic32BytesHexNoPrefix + "6000" + "6000" + "a1";
        }

        // Independent reimplementation of the standard Ethereum block bloom-filter
        // "add" rule (yellow paper's M3:2048): hash the item, take three 11-bit
        // indices from the hash's first six bytes, and set those bits. This is a
        // from-scratch computation in the test -- not a call into
        // BlockProducer/TransactionProcessor's own bloom code -- so it catches a
        // fix that only ORs the LAST call's bloom into the header instead of
        // every call's.
        private static void IndependentlyAddToBloom(byte[] bloom, string hexData)
        {
            var data = hexData.HexToByteArray();
            var hash = new Sha3Keccack().CalculateHash(data);

            for (var i = 0; i < 6; i += 2)
            {
                var bit = ((hash[i] & 0x07) << 8) + hash[i + 1];
                bit &= 0x7FF;
                var byteIndex = 255 - (bit / 8);
                var bitIndex = bit % 8;
                bloom[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
    }
}
