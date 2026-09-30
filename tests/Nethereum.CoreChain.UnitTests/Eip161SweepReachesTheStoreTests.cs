using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class Eip161SweepReachesTheStoreTests
    {
        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
        private const string RevertingContractAddress = "0x0000000000000000000000000000000000000b02";

        private const long BaseFee = 7;
        private static readonly BigInteger ChainId = 1337;

        private static readonly byte[] AlwaysReverts = "60006000fd".HexToByteArray();

        private static ISignedTransaction CallThatReverts() =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                SenderPrivateKey,
                new Transaction1559(ChainId, 0, BaseFee, BaseFee, 200_000, RevertingContractAddress, 0, "", null)));

        private static Account EmptyAccount() => new Account
        {
            Nonce = EvmUInt256.Zero,
            Balance = EvmUInt256.Zero,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH
        };

        private static async Task<InMemoryStateStore> ExecuteAmsterdamBlockAsync(Account feeRecipient)
        {
            var keccak = new Sha3Keccack();
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(FeeRecipientAddress, feeRecipient);

            var codeHash = keccak.CalculateHash(AlwaysReverts);
            await stateStore.SaveCodeAsync(codeHash, AlwaysReverts);
            await stateStore.SaveAccountAsync(RevertingContractAddress, new Account { Balance = 0, Nonce = 0, CodeHash = codeHash });

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = BaseFee,
                Hardfork = nameof(HardforkName.Amsterdam)
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = new BlockExecutor(
                stateStore,
                new InMemoryBlockStore(),
                new FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = 30_000_000,
                BaseFee = BaseFee,
                Coinbase = FeeRecipientAddress,
                ParentHash = new byte[32]
            };

            var result = await engine.ExecuteAsync(
                header, new[] { new TxEntry(CallThatReverts()) }, uncles: null, withdrawals: null,
                options: new BlockExecutionOptions());

            Assert.Null(result.Exception);
            var receipt = Assert.Single(result.Receipts);
            Assert.False(receipt.Success, "the transaction was supposed to fail");
            return stateStore;
        }

        [Fact]
        public async Task Given_AnEmptyFeeRecipient_When_TheOnlyTransactionFails_Then_TheSweepStillRemovesItFromTheStore()
        {
            var stateStore = await ExecuteAmsterdamBlockAsync(EmptyAccount());

            Assert.Null(await stateStore.GetAccountAsync(FeeRecipientAddress));
        }

        [Fact]
        public async Task Given_AFundedFeeRecipient_When_TheOnlyTransactionFails_Then_ItKeepsItsBalance()
        {
            var stateStore = await ExecuteAmsterdamBlockAsync(new Account { Balance = 1_000, Nonce = 0 });

            var feeRecipient = await stateStore.GetAccountAsync(FeeRecipientAddress);
            Assert.NotNull(feeRecipient);
            Assert.Equal(new EvmUInt256(1_000), feeRecipient.Balance);
        }
    }
}
