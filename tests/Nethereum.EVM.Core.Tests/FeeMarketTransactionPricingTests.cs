using System.Collections.Generic;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class FeeMarketTransactionPricingTests
    {
        private const long ChainId = 1;
        private const string Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba";
        private const string Recipient = "0x00000000000000000000000000000000000000e1";
        private const string DelegationTarget = "0x00000000000000000000000000000000000000e2";
        private const string AuthorityKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        private const long BaseFee = 7;

        private static readonly EvmUInt256 MaxFeePerGas = new EvmUInt256(1_000_000_000UL);
        private static readonly EvmUInt256 MaxPriorityFeePerGas = new EvmUInt256(2UL);
        private static readonly EvmUInt256 GasLimit = new EvmUInt256(200_000UL);

        private static string AuthorityAddress => new EthECKey(AuthorityKey).GetPublicAddress();

        private static Authorisation7702Signed Authorisation() =>
            new Authorisation7702Signer().SignAuthorisation(
                AuthorityKey,
                new Authorisation7702
                {
                    ChainId = new EvmUInt256((ulong)ChainId),
                    Address = DelegationTarget,
                    Nonce = EvmUInt256.Zero
                });

        private static BlockWitnessTransaction Type4(EvmUInt256 maxPriorityFeePerGas)
        {
            var transaction = new Transaction7702(
                chainId: new EvmUInt256((ulong)ChainId),
                nonce: EvmUInt256.Zero,
                maxPriorityFeePerGas: maxPriorityFeePerGas,
                maxFeePerGas: MaxFeePerGas,
                gasLimit: GasLimit,
                receiverAddress: Recipient,
                amount: EvmUInt256.Zero,
                data: "0x",
                accessList: new List<AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed> { Authorisation() });

            return new BlockWitnessTransaction
            {
                From = TestTransactionHelper.GetDefaultSenderAddress(),
                RlpEncoded = new Transaction7702Signer()
                    .SignTransaction(TestTransactionHelper.DefaultPrivateKey, transaction).HexToByteArray(),
                AuthorisationAuthorities = new List<string> { AuthorityAddress }
            };
        }

        private static BlockWitnessTransaction Type2(EvmUInt256 maxPriorityFeePerGas)
        {
            var transaction = new Transaction1559(
                chainId: new EvmUInt256((ulong)ChainId),
                nonce: EvmUInt256.Zero,
                maxPriorityFeePerGas: maxPriorityFeePerGas,
                maxFeePerGas: MaxFeePerGas,
                gasLimit: GasLimit,
                receiverAddress: Recipient,
                amount: EvmUInt256.Zero,
                data: "0x",
                accessList: new List<AccessListItem>());

            return new BlockWitnessTransaction
            {
                From = TestTransactionHelper.GetDefaultSenderAddress(),
                RlpEncoded = new Transaction1559Signer()
                    .SignTransaction(TestTransactionHelper.DefaultPrivateKey, transaction).HexToByteArray()
            };
        }

        private static BlockExecutionResult Execute(BlockWitnessTransaction transaction)
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = BaseFee,
                BlockGasLimit = 30_000_000,
                ChainId = ChainId,
                Coinbase = Coinbase,
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction> { transaction },
                Accounts = new List<WitnessAccount>
                {
                    Account(TestTransactionHelper.GetDefaultSenderAddress(), new EvmUInt256(1_000_000_000_000_000_000UL)),
                    Account(Recipient, EvmUInt256.Zero),
                    Account(Coinbase, EvmUInt256.Zero),
                    Account(AuthorityAddress, EvmUInt256.Zero),
                    Account(DelegationTarget, EvmUInt256.Zero)
                }
            };

            return BlockExecutionHelper.ExecuteBlock(block);
        }

        private static WitnessAccount Account(string address, EvmUInt256 balance) =>
            new WitnessAccount
            {
                Address = address,
                Balance = balance,
                Nonce = 0,
                Code = new byte[0],
                Storage = new List<WitnessStorageSlot>()
            };

        private static void AssertCoinbasePaidPriorityFeeOnly(BlockExecutionResult result)
        {
            var transactionResult = Assert.Single(result.TxResults);
            Assert.False(transactionResult.IsValidationError, transactionResult.Error);

            var expected = new EvmUInt256((ulong)transactionResult.GasUsed) * MaxPriorityFeePerGas;
            Assert.Equal(expected, result.StateReader.GetBalance(Coinbase));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AType4Transaction_When_TheBlockIsExecutedFromAWitness_Then_TheCoinbaseIsPaidThePriorityFeeOnly()
        {
            AssertCoinbasePaidPriorityFeeOnly(Execute(Type4(MaxPriorityFeePerGas)));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AType2Transaction_When_TheBlockIsExecutedFromAWitness_Then_ItIsPricedUnchanged()
        {
            AssertCoinbasePaidPriorityFeeOnly(Execute(Type2(MaxPriorityFeePerGas)));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AType4TransactionWhosePriorityFeeExceedsItsMaxFee_When_Validated_Then_ItIsRejected()
        {
            var result = Execute(Type4(MaxFeePerGas + EvmUInt256.One));

            var transactionResult = Assert.Single(result.TxResults);
            Assert.True(transactionResult.IsValidationError);
            Assert.Equal(TransactionError.PriorityGreaterThanMaxFee, transactionResult.ErrorCode);
        }
    }
}
