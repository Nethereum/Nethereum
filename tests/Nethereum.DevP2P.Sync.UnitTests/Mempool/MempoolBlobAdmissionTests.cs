using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.Mempool
{
    public class MempoolBlobAdmissionTests
    {
        private const string PrivateKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int NodeChainId = 1;

        private static readonly MockBlobKzgProvider Kzg = new();

        private static string SenderAddress => new EthECKey(PrivateKey).GetPublicAddress().ToLowerInvariant();

        private static MempoolAdmissionValidator Validator() => new MempoolAdmissionValidator(NodeChainId);

        private static async Task<InMemoryChainStoreBundle> FundedBundleAsync(BigInteger nonce, BigInteger balance)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account
            {
                Nonce = (EvmUInt256)nonce,
                Balance = (EvmUInt256)balance
            });
            return bundle;
        }

        private static byte[] MakeBlob(byte seed)
        {
            var blob = new byte[BlobEncoder.BLOB_SIZE];
            blob[1] = seed;
            return blob;
        }

        private static (BlobSidecar Sidecar, List<byte[]> VersionedHashes) BuildSidecar(int blobCount)
        {
            var blobs = new List<byte[]>();
            var commitments = new List<byte[]>();
            var proofs = new List<byte[]>();
            var versionedHashes = new List<byte[]>();
            for (int i = 0; i < blobCount; i++)
            {
                var blob = MakeBlob((byte)(i + 1));
                var commitment = Kzg.BlobToKzgCommitment(blob);
                blobs.Add(blob);
                commitments.Add(commitment);
                proofs.Add(Kzg.ComputeBlobKzgProof(blob, commitment));
                versionedHashes.Add(Kzg.ComputeVersionedHash(commitment));
            }
            return (new BlobSidecar(blobs, commitments, proofs), versionedHashes);
        }

        private static Transaction4844 SignBlobTx(
            BigInteger nonce, BigInteger maxFeePerGas, BigInteger maxFeePerBlobGas,
            BigInteger amount, List<byte[]> versionedHashes, BlobSidecar sidecar,
            BigInteger? gasLimit = null)
        {
            var tx = new Transaction4844(
                chainId: (EvmUInt256)NodeChainId,
                nonce: (EvmUInt256)nonce,
                maxPriorityFeePerGas: (EvmUInt256)1,
                maxFeePerGas: (EvmUInt256)maxFeePerGas,
                gasLimit: (EvmUInt256)(gasLimit ?? 100_000),
                receiverAddress: Recipient,
                amount: (EvmUInt256)amount,
                data: "0x",
                accessList: new List<AccessListItem>(),
                maxFeePerBlobGas: (EvmUInt256)maxFeePerBlobGas,
                blobVersionedHashes: versionedHashes);
            new Transaction4844Signer().SignTransaction(new EthECKey(PrivateKey), tx);
            tx.Sidecar = sidecar;
            return tx;
        }

        [Fact]
        public async Task Given_BlobTxFundedForValueGasAndBlobGas_When_Validated_Then_Accepted()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000);
            var (sidecar, hashes) = BuildSidecar(blobCount: 1);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(MempoolRejectReason.None, result.Reason);
            Assert.Equal(SenderAddress, result.Sender);
        }

        [Fact]
        public async Task Given_BlobTxFundedForGasButNotBlobGas_When_Validated_Then_RejectedInsufficientBalance()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 100_000);
            var (sidecar, hashes) = BuildSidecar(blobCount: 1);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.InsufficientBalance, result.Reason);
        }

        [Fact]
        public async Task Given_BlobTxWithoutSidecar_When_Validated_Then_RejectedBlobSidecarMissing()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000);
            var (_, hashes) = BuildSidecar(blobCount: 1);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar: null);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.BlobSidecarMissing, result.Reason);
        }

        [Fact]
        public async Task Given_BlobTxWithZeroBlobs_When_Validated_Then_RejectedBlobCountInvalid()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000);
            var (sidecar, _) = BuildSidecar(blobCount: 1);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0,
                versionedHashes: new List<byte[]>(), sidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.BlobCountInvalid, result.Reason);
        }

        [Fact]
        public async Task Given_BlobTxAbovePerTxCap_When_Validated_Then_RejectedBlobCountInvalid()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: BigInteger.Pow(10, 30));
            var (sidecar, hashes) = BuildSidecar(blobCount: 7);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.BlobCountInvalid, result.Reason);
        }

        [Fact]
        public async Task Given_BlobTxWhoseCommitmentDoesNotDeriveTheHash_When_Validated_Then_RejectedBlobVersionedHashInvalid()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000);
            var (_, hashes) = BuildSidecar(blobCount: 1);
            var (wrongSidecar, _) = BuildSidecar(blobCount: 1);
            wrongSidecar.Commitments[0] = Kzg.BlobToKzgCommitment(MakeBlob(0x99));
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, wrongSidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.BlobVersionedHashInvalid, result.Reason);
        }

        [Fact]
        public async Task Given_BlobTxWithWrongVersionedHashPrefix_When_Validated_Then_RejectedBlobVersionedHashInvalid()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000);
            var (sidecar, hashes) = BuildSidecar(blobCount: 1);
            hashes[0][0] = 0x02;
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.BlobVersionedHashInvalid, result.Reason);
        }

        [Fact]
        public async Task Given_LargeMultiBlobNetworkingForm_When_Validated_Then_AcceptedNotOversize()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: BigInteger.Pow(10, 30));
            var (sidecar, hashes) = BuildSidecar(blobCount: 6);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            Assert.True(tx.GetRLPEncodedWithSidecar().Length > MempoolAdmissionValidator.DefaultMaxTransactionSizeBytes);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(MempoolRejectReason.None, result.Reason);
        }

        [Fact]
        public void PooledTransactions_ServesTheSidecar_ForType3_RoundTrip()
        {
            var (sidecar, hashes) = BuildSidecar(blobCount: 2);
            var tx = SignBlobTx(nonce: 0, maxFeePerGas: 1, maxFeePerBlobGas: 1, amount: 0, hashes, sidecar);

            var msg = new PooledTransactionsMessage { RequestId = 7 };
            msg.Transactions.Add(tx);
            var encoded = PooledTransactionsMessageEncoder.Encode(msg);
            var decoded = PooledTransactionsMessageEncoder.Decode(encoded);

            var roundTripped = Assert.IsType<Transaction4844>(Assert.Single(decoded.Transactions));
            Assert.NotNull(roundTripped.Sidecar);
            Assert.Equal(2, roundTripped.Sidecar.Blobs.Count);
            Assert.Equal(2, roundTripped.Sidecar.Commitments.Count);
            Assert.Equal(2, roundTripped.Sidecar.Proofs.Count);
            Assert.Equal(BlobEncoder.BLOB_SIZE, roundTripped.Sidecar.Blobs[0].Length);
        }
    }
}
