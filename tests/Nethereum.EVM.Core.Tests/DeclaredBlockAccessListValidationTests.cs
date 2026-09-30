using System.Collections.Generic;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class DeclaredBlockAccessListValidationTests
    {
        private const string SenderKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string Sender = TestTransactionHelper.GetDefaultSenderAddress();
        private const string Recipient = "0x1111111111111111111111111111111111111111";

        private const string LowerAddress = "0x0000000000000000000000000000000000000aaa";
        private const string HigherAddress = "0x0000000000000000000000000000000000000bbb";

        // The block below carries ONE transaction, so EIP-7928's inclusive bound —
        // "BAL indices ... MUST never be higher than len(transactions) + 1" — is 2.
        private const ulong HighestIndexForOneTransaction = 2;

        private static List<AccountChanges> AccountsInOrder()
        {
            return new List<AccountChanges>
            {
                new AccountChanges(LowerAddress),
                new AccountChanges(HigherAddress)
            };
        }

        private static List<AccountChanges> AccountsOutOfOrder()
        {
            return new List<AccountChanges>
            {
                new AccountChanges(HigherAddress),
                new AccountChanges(LowerAddress)
            };
        }

        private static List<AccountChanges> AccountWithBalanceChangeAt(ulong blockAccessIndex)
        {
            var account = new AccountChanges(LowerAddress);
            account.BalanceChanges.Add(new BalanceChange(blockAccessIndex, new EvmUInt256(1)));
            return new List<AccountChanges> { account };
        }

        private static BlockExecutionResult ExecuteBlockDeclaring(List<AccountChanges> declaredBlockAccessList)
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                BaseFee = 7,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                DeclaredBlockAccessList = declaredBlockAccessList,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(
                        Recipient, EvmUInt256.Zero, 0, 10, 100_000, SenderKey)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = Sender,
                        Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = Recipient,
                        Balance = EvmUInt256.Zero,
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    }
                }
            };

            var result = BlockExecutionHelper.ExecuteBlock(block);
            var transaction = Assert.Single(result.TxResults);
            Assert.True(transaction.Success, transaction.Error);
            return result;
        }

        [Fact]
        [Trait("Rule", "AMS-7928-35")]
        public void Given_ADeclaredListWhoseAccountsAreOutOfOrder_When_TheBlockExecutes_Then_ItIsMalformed()
        {
            var result = ExecuteBlockDeclaring(AccountsOutOfOrder());

            Assert.True(result.BlockAccessListMalformed);
            Assert.Equal(BlockAccessListStructureViolation.AccountsOutOfOrder,
                result.DeclaredBlockAccessListCheck.Violation);
            Assert.Contains(HigherAddress, result.DeclaredBlockAccessListCheck.Description);
        }

        [Fact]
        [Trait("Rule", "AMS-7928-35")]
        public void Given_AWellFormedDeclaredList_When_TheBlockExecutes_Then_ItIsNotMalformed()
        {
            var result = ExecuteBlockDeclaring(AccountsInOrder());

            Assert.False(result.BlockAccessListMalformed);
            Assert.Equal(BlockAccessListStructureViolation.None,
                result.DeclaredBlockAccessListCheck.Violation);
        }

        [Fact]
        [Trait("Rule", "AMS-7928-35")]
        public void Given_NoDeclaredList_When_TheBlockExecutes_Then_ItIsNotMalformed()
        {
            var result = ExecuteBlockDeclaring(null);

            Assert.False(result.BlockAccessListMalformed);
            Assert.Equal(BlockAccessListStructureViolation.None,
                result.DeclaredBlockAccessListCheck.Violation);
        }

        [Fact]
        [Trait("Rule", "AMS-7928-35")]
        public void Given_ABlockAccessIndexAtTheTransactionCountBound_When_TheBlockExecutes_Then_ItIsNotMalformed()
        {
            var result = ExecuteBlockDeclaring(
                AccountWithBalanceChangeAt(HighestIndexForOneTransaction));

            Assert.False(result.BlockAccessListMalformed);
            Assert.Equal(BlockAccessListStructureViolation.None,
                result.DeclaredBlockAccessListCheck.Violation);
        }

        [Fact]
        [Trait("Rule", "AMS-7928-35")]
        public void Given_ABlockAccessIndexAboveTheTransactionCountBound_When_TheBlockExecutes_Then_ItIsMalformed()
        {
            var result = ExecuteBlockDeclaring(
                AccountWithBalanceChangeAt(HighestIndexForOneTransaction + 1));

            Assert.True(result.BlockAccessListMalformed);
            Assert.Equal(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                result.DeclaredBlockAccessListCheck.Violation);
        }
    }
}
