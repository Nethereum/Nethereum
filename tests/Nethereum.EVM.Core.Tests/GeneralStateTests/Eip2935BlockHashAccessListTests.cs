using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class Eip2935BlockHashAccessListTests
    {
        private const string HistoryContractRuntimeCode =
            "3373fffffffffffffffffffffffffffffffffffffffe14604657602036036042575f356001430381116042576" +
            "11fff81430311604257611fff9006545f5260205ff35b5f5ffd5b5f35611fff60014303065500";

        private const string DriverAddress = "0x000000000000000000000000000000000000dead";

        private const long BlockNumber = 300;
        private const long ParentBlockNumber = BlockNumber - 1;
        private const long QueriedBlockNumber = 100;

        private static BlockExecutionResult ExecuteBlockhashBlock()
        {
            var parentHash = Enumerable.Repeat((byte)0x11, 32).ToArray();

            var driverCode = "6064" + "40" + "50" + "00";

            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var tx = TestTransactionHelper.CreateSignedContractCall(
                DriverAddress,
                data: new byte[0],
                value: EvmUInt256.Zero,
                nonce: EvmUInt256.Zero,
                gasPrice: new EvmUInt256(1UL),
                gasLimit: new EvmUInt256(200000UL));

            var block = new BlockWitnessData
            {
                BlockNumber = BlockNumber,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = parentHash,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction> { tx },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = sender,
                        Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = DriverAddress,
                        Balance = EvmUInt256.Zero,
                        Nonce = 1,
                        Code = driverCode.HexToByteArray(),
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = Eip2935BlockHashRule.HISTORY_STORAGE_ADDRESS,
                        Balance = EvmUInt256.Zero,
                        Nonce = 1,
                        Code = HistoryContractRuntimeCode.HexToByteArray(),
                        Storage = new List<WitnessStorageSlot>
                        {
                            new WitnessStorageSlot
                            {
                                Key = new EvmUInt256((ulong)QueriedBlockNumber),
                                Value = new EvmUInt256(0x1234UL)
                            }
                        }
                    }
                }
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ATransactionExecutingBlockhash_When_TheBlockAccessListIsBuilt_Then_TheHistoryContractIsNotRecordedAsARead()
        {
            var result = ExecuteBlockhashBlock();

            Assert.True(result.TxResults.Single().Success,
                $"driver transaction failed: {result.TxResults.Single().RevertReason}");
            Assert.NotNull(result.BlockAccessList);

            var history = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, Eip2935BlockHashRule.HISTORY_STORAGE_ADDRESS, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(history != null, "the history contract is absent from the access list entirely");

            var queriedSlot = new EvmUInt256((ulong)QueriedBlockNumber);
            Assert.DoesNotContain(history.StorageReads, s => s.Equals(queriedSlot));
            Assert.DoesNotContain(history.StorageChanges, c => c.Slot.Equals(queriedSlot));
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_TheHistoryContractsOwnSystemCall_When_TheBlockAccessListIsBuilt_Then_ItIsRecordedAsAWriteAtIndexZero()
        {
            var result = ExecuteBlockhashBlock();

            var history = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, Eip2935BlockHashRule.HISTORY_STORAGE_ADDRESS, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(history != null, "the history contract is absent from the access list entirely");

            var parentSlot = new EvmUInt256((ulong)ParentBlockNumber);
            var slotChanges = Assert.Single(history.StorageChanges, c => c.Slot.Equals(parentSlot));
            var change = Assert.Single(slotChanges.Changes);
            Assert.Equal(0UL, change.BlockAccessIndex);
        }
    }
}
