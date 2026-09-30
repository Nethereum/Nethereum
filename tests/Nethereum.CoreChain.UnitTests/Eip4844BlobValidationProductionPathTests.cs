using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class Eip4844BlobValidationProductionPathTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        private const string Prague = "prague";
        private const string Amsterdam = "amsterdam";

        private static readonly BigInteger ChainId = 1337;

        private static readonly Transaction4844Signer Signer = new();

        private static async Task<TransactionProcessor> BuildProcessorAsync(ChainConfig config)
        {
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAccountAsync(SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });

            return new TransactionProcessor(
                stateStore, new InMemoryBlockStore(), config, new TransactionVerificationAndRecoveryImp());
        }

        private static byte[] VersionedHash()
        {
            var hash = new byte[32];
            hash[0] = 0x01;
            return hash;
        }

        private static ISignedTransaction BlobTransaction(
            string receiverAddress,
            List<byte[]> blobVersionedHashes,
            EvmUInt256? maxFeePerBlobGas = null)
        {
            var transaction = new Transaction4844(
                chainId: new EvmUInt256((ulong)ChainId),
                nonce: EvmUInt256.Zero,
                maxPriorityFeePerGas: EvmUInt256.One,
                maxFeePerGas: EvmUInt256.One,
                gasLimit: new EvmUInt256(100_000UL),
                receiverAddress: receiverAddress,
                amount: EvmUInt256.Zero,
                data: "0x",
                accessList: new List<AccessListItem>(),
                maxFeePerBlobGas: maxFeePerBlobGas ?? EvmUInt256.One,
                blobVersionedHashes: blobVersionedHashes);

            return TransactionFactory.CreateTransaction(
                Signer.SignTransaction(PrivateKey, transaction).HexToByteArray());
        }

        private static async Task<TransactionExecutionResult> ExecuteAsFollowerAsync(
            string hardfork, ISignedTransaction transaction)
        {
            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                Hardfork = hardfork
            };
            var processor = await BuildProcessorAsync(config);
            var blockContext = BlockContext.FromConfig(config, blockNumber: 1, timestamp: 1_700_000_000);

            return await processor.ExecuteTransactionAsync(
                transaction, blockContext, txIndex: 0, cumulativeGasUsed: 0);
        }

        [Theory]
        [InlineData(Prague)]
        [InlineData(Amsterdam)]
        public async Task Given_ATypeThreeTransactionWithNoBlobs_When_ExecutedByTheFollower_Then_ItIsRejectedWithZeroBlobs(string hardfork)
        {
            var result = await ExecuteAsFollowerAsync(
                hardfork, BlobTransaction(RecipientAddress, new List<byte[]>()));

            Assert.False(result.Success);
            Assert.Equal(TransactionError.Type3TxZeroBlobs, result.ErrorCode);
        }

        [Theory]
        [InlineData(Prague)]
        [InlineData(Amsterdam)]
        public async Task Given_ATypeThreeTransactionWithOneBlob_When_ExecutedByTheFollower_Then_ItIsAccepted(string hardfork)
        {
            var result = await ExecuteAsFollowerAsync(
                hardfork, BlobTransaction(RecipientAddress, new List<byte[]> { VersionedHash() }));

            Assert.True(result.Success, result.RevertReason);
            Assert.Equal(TransactionError.None, result.ErrorCode);
        }

        [Theory]
        [InlineData(Prague)]
        [InlineData(Amsterdam)]
        public async Task Given_ATypeThreeTransactionWithNoBlobsAndNoRecipient_When_ExecutedByTheFollower_Then_ItIsRejectedWithZeroBlobs(string hardfork)
        {
            var result = await ExecuteAsFollowerAsync(hardfork, BlobTransaction("", new List<byte[]>()));

            Assert.False(result.Success);
            Assert.Equal(TransactionError.Type3TxZeroBlobs, result.ErrorCode);
        }

        [Theory]
        [InlineData(Prague)]
        [InlineData(Amsterdam)]
        public async Task Given_ATypeThreeTransactionWithOneBlobAndNoRecipient_When_ExecutedByTheFollower_Then_ItIsRejectedAsContractCreation(string hardfork)
        {
            var result = await ExecuteAsFollowerAsync(
                hardfork, BlobTransaction("", new List<byte[]> { VersionedHash() }));

            Assert.False(result.Success);
            Assert.Equal(TransactionError.Type3TxContractCreation, result.ErrorCode);
        }

        [Theory]
        [InlineData(Prague)]
        [InlineData(Amsterdam)]
        public async Task Given_ATypeThreeTransactionWithNoBlobsAndZeroMaxFeePerBlobGas_When_ExecutedByTheFollower_Then_ItIsRejectedForTheUnderpricedBlobFee(string hardfork)
        {
            var result = await ExecuteAsFollowerAsync(
                hardfork, BlobTransaction(RecipientAddress, new List<byte[]>(), maxFeePerBlobGas: EvmUInt256.Zero));

            Assert.False(result.Success);
            Assert.Equal(TransactionError.InsufficientMaxFeePerBlobGas, result.ErrorCode);
        }
    }
}
