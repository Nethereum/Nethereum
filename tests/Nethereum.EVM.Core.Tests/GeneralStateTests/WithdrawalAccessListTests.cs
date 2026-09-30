using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class WithdrawalAccessListTests
    {
        private const string Recipient = "0x000000000000000000000000000000000000c1e0";

        private static BlockWitnessData BlockWithOneWithdrawal(ulong amountInGwei)
        {
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
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction>(),
                Accounts = new List<WitnessAccount>(),
                Withdrawals = new List<BlockWithdrawal>
                {
                    new BlockWithdrawal
                    {
                        Index = 0,
                        ValidatorIndex = 0,
                        Address = Recipient,
                        AmountInGwei = amountInGwei
                    }
                }
            };
        }

        private static BlockExecutionResult Run(ulong amountInGwei)
        {
            return BlockExecutor.Execute(
                BlockWithOneWithdrawal(amountInGwei).AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AZeroAmountWithdrawal_When_TheBlockAccessListIsBuilt_Then_TheRecipientIsPresentWithNoChanges()
        {
            var result = Run(amountInGwei: 0);

            Assert.NotNull(result.BlockAccessList);
            var entry = result.BlockAccessList.SingleOrDefault(
                a => string.Equals(a.Address, Recipient, System.StringComparison.OrdinalIgnoreCase));

            Assert.True(entry != null, "the zero-amount withdrawal recipient is absent from the block access list");
            Assert.Empty(entry.BalanceChanges);
            Assert.Empty(entry.NonceChanges);
            Assert.Empty(entry.CodeChanges);
            Assert.Empty(entry.StorageChanges);
            Assert.Empty(entry.StorageReads);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ANonZeroAmountWithdrawal_When_TheBlockAccessListIsBuilt_Then_TheRecipientsBalanceChangeIsRecorded()
        {
            var result = Run(amountInGwei: 5);

            Assert.NotNull(result.BlockAccessList);
            var entry = result.BlockAccessList.SingleOrDefault(
                a => string.Equals(a.Address, Recipient, System.StringComparison.OrdinalIgnoreCase));

            Assert.True(entry != null, "the withdrawal recipient is absent from the block access list");
            var change = Assert.Single(entry.BalanceChanges);
            Assert.Equal(new Nethereum.Util.EvmUInt256(5UL) * new Nethereum.Util.EvmUInt256(1000000000UL), change.PostBalance);
        }
    }
}
