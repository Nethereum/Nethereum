using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class CrossTransactionStorageVisibilityTests
    {
        private readonly ITestOutputHelper _output;

        public CrossTransactionStorageVisibilityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private const string Counter = "0x00000000000000000000000000000000000c0de1";

        private static byte[] IncrementSlotZero() => "60005460010160005500".HexToByteArray();

        private static BlockWitnessData BlockCalling(int times, EvmUInt256 slotZeroBefore)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var txs = new List<BlockWitnessTransaction>();
            for (var i = 0; i < times; i++)
                txs.Add(TestTransactionHelper.CreateSignedTransfer(
                    Counter, EvmUInt256.Zero, new EvmUInt256((ulong)i), new EvmUInt256(10), new EvmUInt256(100000)));

            return new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 7,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = BlockFeatureConfig.Cancun,
                Transactions = txs,
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = sender,
                        Balance = new EvmUInt256(10000000),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = Counter,
                        Balance = EvmUInt256.Zero,
                        Nonce = 0,
                        Code = IncrementSlotZero(),
                        Storage = new List<WitnessStorageSlot>
                        {
                            new WitnessStorageSlot { Key = EvmUInt256.Zero, Value = slotZeroBefore }
                        }
                    }
                }
            };
        }

        private static Nethereum.EVM.Execution.BlockExecutionResult Run(BlockWitnessData block)
        {
            return Nethereum.EVM.Execution.BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance);
        }

        private EvmUInt256 SlotZeroAfter(Nethereum.EVM.Execution.BlockExecutionResult result)
        {
            for (var i = 0; i < result.TxResults.Count; i++)
                _output.WriteLine($"tx[{i}]: success={result.TxResults[i].Success} gas={result.TxResults[i].GasUsed} error={result.TxResults[i].Error}");

            foreach (var tx in result.TxResults)
                Assert.True(tx.Success, $"transaction failed: {tx.Error}");

            var account = result.StateReader.GetAccountState(Counter);
            Assert.True(account != null, "the counter account is absent from the post-block state");
            Assert.True(account.Storage.TryGetValue(EvmUInt256.Zero, out var raw),
                "slot 0 is absent from the post-block state");
            return EvmUInt256.FromBigEndian(raw);
        }

        [Fact]
        [Trait("Category", "BlockExecution")]
        public void Given_AnAccountWithStorage_When_Loaded_Then_ItsSlotsArePresentToBeCounted()
        {
            var accounts = WitnessStateBuilder.BuildAccountState(
                BlockCalling(times: 0, slotZeroBefore: new EvmUInt256(5)).Accounts);
            var reader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(reader);

            WitnessStateBuilder.LoadAllAccountsAndStorage(
                executionState, reader, BlockCalling(times: 0, slotZeroBefore: new EvmUInt256(5)).Accounts);

            var loaded = executionState.CreateOrGetAccountExecutionState(Counter);
            Assert.True(loaded.Storage.Count > 0,
                "the loaded account reports no storage, so create-collision detection cannot see it");
        }

        [Fact]
        [Trait("Category", "BlockExecution")]
        public void Given_OneTransactionWritesASlot_When_TheBlockCompletes_Then_TheWriteIsInTheState()
        {
            var result = Run(BlockCalling(times: 1, slotZeroBefore: new EvmUInt256(5)));

            Assert.Equal(new EvmUInt256(6), SlotZeroAfter(result));
        }

        [Fact]
        [Trait("Category", "BlockExecution")]
        public void Given_TwoTransactionsIncrementTheSameSlot_When_TheBlockCompletes_Then_BothIncrementsApplied()
        {
            var result = Run(BlockCalling(times: 2, slotZeroBefore: new EvmUInt256(5)));

            Assert.Equal(new EvmUInt256(7), SlotZeroAfter(result));
        }

        private static byte[] StoreCallDataToSlotZero() => "60003560005500".HexToByteArray();

        private static byte[] Word(byte value)
        {
            var word = new byte[32];
            word[31] = value;
            return word;
        }

        [Fact]
        [Trait("Category", "BlockExecution")]
        public void Given_ATransactionRestoresThePreBlockValue_When_Priced_Then_ItIsNotBilledAsANoOp()
        {
            var block = BlockCalling(times: 0, slotZeroBefore: new EvmUInt256(5));
            block.Accounts[1].Code = StoreCallDataToSlotZero();
            block.Transactions.Add(TestTransactionHelper.CreateSignedContractCall(
                Counter, Word(9), EvmUInt256.Zero, EvmUInt256.Zero, new EvmUInt256(10), new EvmUInt256(100000)));
            block.Transactions.Add(TestTransactionHelper.CreateSignedContractCall(
                Counter, Word(5), EvmUInt256.Zero, new EvmUInt256(1), new EvmUInt256(10), new EvmUInt256(100000)));

            var result = Run(block);

            _output.WriteLine($"tx[0] wrote 9: gas={result.TxResults[0].GasUsed}");
            _output.WriteLine($"tx[1] wrote 5: gas={result.TxResults[1].GasUsed}");
            foreach (var tx in result.TxResults)
                Assert.True(tx.Success, $"transaction failed: {tx.Error}");

            Assert.Equal(result.TxResults[0].GasUsed, result.TxResults[1].GasUsed);
            Assert.Equal(new EvmUInt256(5), SlotZeroAfter(result));
        }
    }
}
