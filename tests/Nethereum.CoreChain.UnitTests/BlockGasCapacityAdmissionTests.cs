using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockGasCapacityAdmissionTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string FreshRecipientAddress = "0x00000000000000000000000000000000000ba1a5";
        private const string CoinbaseAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
        private const long TransferGasLimit = 21_000;

        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static ISignedTransaction Transfer(string to, BigInteger nonce, BigInteger gasLimit) =>
            TransactionFactory.CreateTransaction(Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, to, 100, nonce, 0, gasLimit, ""));

        private static async Task<BlockExecutionResult> ValidateBlockAsync(
            HardforkName fork, long blockGasLimit, params TxEntry[] txs)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(RecipientAddress, new Account { Balance = 1, Nonce = 0 });

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = blockGasLimit,
                BaseFee = 0,
                Hardfork = fork.ToString()
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = blockGasLimit,
                BaseFee = 0,
                BlobGasUsed = 0,
                ExcessBlobGas = 0,
                Coinbase = CoinbaseAddress,
                ParentHash = new byte[32]
            };

            var result = await engine.ExecuteAsync(
                header, txs, uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.Null(result.Exception);
            return result;
        }

        [Fact]
        public async Task Given_TwoTransactionsWhoseCombinedGasExceedsTheBlockLimit_When_Imported_Then_TheBlockIsRejected()
        {
            var result = await ValidateBlockAsync(
                HardforkName.Prague,
                blockGasLimit: 2 * TransferGasLimit - 1,
                new TxEntry(Transfer(RecipientAddress, nonce: 0, TransferGasLimit)),
                new TxEntry(Transfer(RecipientAddress, nonce: 1, TransferGasLimit)));

            Assert.True(result.ContainsInvalidTransaction);
            Assert.Equal(TransactionError.GasAllowanceExceeded, result.InvalidTransactionReason);
            Assert.Equal(1, result.InvalidTransactionIndex);
        }

        [Fact]
        public async Task Given_TwoTransactionsWhoseCombinedGasFitsExactly_When_Imported_Then_TheBlockIsAccepted()
        {
            var result = await ValidateBlockAsync(
                HardforkName.Prague,
                blockGasLimit: 2 * TransferGasLimit,
                new TxEntry(Transfer(RecipientAddress, nonce: 0, TransferGasLimit)),
                new TxEntry(Transfer(RecipientAddress, nonce: 1, TransferGasLimit)));

            Assert.False(result.ContainsInvalidTransaction);
            Assert.Equal(2 * TransferGasLimit, result.BlockExecutionGasUsed);
        }

        [Fact]
        public async Task Given_ATransactionExceedingRemainingStateGasOnly_When_Imported_Then_TheBlockIsRejected()
        {
            const long blockGasLimit = 400_000;
            const long secondTxGasLimit = 250_000;

            var accountCreation = await ValidateBlockAsync(
                HardforkName.Amsterdam, blockGasLimit,
                new TxEntry(Transfer(FreshRecipientAddress, nonce: 0, secondTxGasLimit)));

            Assert.False(accountCreation.ContainsInvalidTransaction);
            Assert.True(secondTxGasLimit <= blockGasLimit - accountCreation.BlockExecutionGasUsed,
                "the execution dimension must still have room, or this proves nothing about the state dimension");
            Assert.True(secondTxGasLimit > blockGasLimit - accountCreation.BlockStateGasUsed,
                "the first transaction must draw the state dimension below the second transaction's gas limit");

            var result = await ValidateBlockAsync(
                HardforkName.Amsterdam, blockGasLimit,
                new TxEntry(Transfer(FreshRecipientAddress, nonce: 0, secondTxGasLimit)),
                new TxEntry(Transfer(RecipientAddress, nonce: 1, secondTxGasLimit)));

            Assert.True(result.ContainsInvalidTransaction);
            Assert.Equal(TransactionError.GasAllowanceExceeded, result.InvalidTransactionReason);
            Assert.Equal(1, result.InvalidTransactionIndex);
        }
    }
}
