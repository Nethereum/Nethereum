using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.Mempool
{
    public class RelayMempoolPolicyTests
    {
        private const string PrivateKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int ChainId = 1;

        private static readonly LegacyTransactionSigner LegacySigner = new();

        private static string SenderAddress => new EthECKey(PrivateKey).GetPublicAddress().ToLowerInvariant();

        private static ISignedTransaction ATransaction(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(LegacySigner.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, Recipient,
                amount: 1000, nonce: nonce, gasPrice: 1, gasLimit: 21_000, data: ""));

        private static async Task<(RelayMempool Mempool, InMemoryChainStoreBundle Bundle)> MempoolAsync(
            MempoolRetention retention)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)BigInteger.Parse("1000000000000000000")
            });

            var mempool = new RelayMempool(
                new TxPool(),
                new Eth68PeerPool(),
                bundle,
                new MempoolAdmissionValidator(ChainId),
                logger: null,
                retention: retention,
                relay: MempoolRelay.Ours);

            return (mempool, bundle);
        }

        [Fact]
        public async Task Given_RelayAllWithoutFullRetention_When_TheMempoolIsBuilt_Then_ItIsRefused()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            using var _ = bundle;

            Assert.Throws<System.ArgumentException>(() => new RelayMempool(
                new TxPool(), new Eth68PeerPool(), bundle, new MempoolAdmissionValidator(ChainId),
                logger: null, retention: MempoolRetention.RelayOnly, relay: MempoolRelay.All));
        }

        [Fact]
        public async Task Given_RelayOnlyRetention_When_APeerTransactionIsAdmitted_Then_ItIsNotOfferedForBlockBuilding()
        {
            var (mempool, bundle) = await MempoolAsync(MempoolRetention.RelayOnly);
            using var _ = bundle;

            var admitted = await mempool.AdmitFromTrustedPeerAsync(new List<ISignedTransaction> { ATransaction(0) });

            Assert.Equal(1, admitted);
            Assert.Empty(await mempool.GetPendingAsync(10));
        }

        [Fact]
        public async Task Given_FullRetention_When_APeerTransactionIsAdmitted_Then_ItIsOfferedForBlockBuilding()
        {
            var (mempool, bundle) = await MempoolAsync(MempoolRetention.Full);
            using var _ = bundle;

            var admitted = await mempool.AdmitFromTrustedPeerAsync(new List<ISignedTransaction> { ATransaction(0) });

            Assert.Equal(1, admitted);
            Assert.Single(await mempool.GetPendingAsync(10));
        }

        [Fact]
        public async Task Given_RelayOnlyRetention_When_ALocalTransactionIsSubmitted_Then_ItIsStillRetainedForItsOwner()
        {
            var (mempool, bundle) = await MempoolAsync(MempoolRetention.RelayOnly);
            using var _ = bundle;

            var result = await mempool.SubmitAsync(ATransaction(0));

            Assert.True(result.Accepted, result.RejectReason.ToString());
            Assert.Single(await mempool.GetPendingAsync(10));
        }
    }
}
