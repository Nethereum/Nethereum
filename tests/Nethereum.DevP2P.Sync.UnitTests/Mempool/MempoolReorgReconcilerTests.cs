using System.Collections.Generic;
using System.Numerics;
using System.Threading;
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
    public class MempoolReorgReconcilerTests
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

        private static byte[] Filled(byte v) => System.Linq.Enumerable.Repeat(v, 32).ToArray();

        private static async Task<(MempoolReorgReconciler Reconciler, ITxPool TxPool, RelayMempool Relay, InMemoryChainStoreBundle Bundle)>
            HarnessAsync()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)BigInteger.Parse("1000000000000000000")
            });

            var txPool = new TxPool();
            var relay = new RelayMempool(
                txPool, new Eth68PeerPool(), bundle, new MempoolAdmissionValidator(ChainId),
                logger: null, retention: MempoolRetention.Full, relay: MempoolRelay.Ours);

            var reconciler = new MempoolReorgReconciler(txPool, relay);
            return (reconciler, txPool, relay, bundle);
        }

        private static async Task SealOrphanedBlockAsync(
            InMemoryChainStoreBundle bundle, ulong number, IReadOnlyList<ISignedTransaction> txs)
        {
            var hash = Filled((byte)number);
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = number }, hash);
            for (var i = 0; i < txs.Count; i++)
                await bundle.Transactions.SaveAsync(txs[i], hash, i, (BigInteger)number);
        }

        [Fact]
        public async Task Given_AReorgFromBranchXToBranchY_When_XsOrphanedBlocksHeldTransactionsNotInY_Then_ThoseTransactionsReturnToThePool()
        {
            var (reconciler, txPool, relay, bundle) = await HarnessAsync();
            using var _ = bundle;

            var orphanedOnlyTx = ATransaction(0);
            await SealOrphanedBlockAsync(bundle, 11, new List<ISignedTransaction> { orphanedOnlyTx });

            await reconciler.ReconcileAsync(bundle, commonAncestor: 10, orphanedHead: 11,
                winningBranchTransactions: new List<ISignedTransaction>(), CancellationToken.None);

            var pending = await relay.GetPendingAsync(10);
            Assert.Contains(pending, tx => tx.Hash.ToHex() == orphanedOnlyTx.Hash.ToHex());
        }

        [Fact]
        public async Task Given_AReorgFromBranchXToBranchY_When_ATransactionIsInBothBranches_Then_ItIsNotDuplicatedInThePool()
        {
            var (reconciler, txPool, relay, bundle) = await HarnessAsync();
            using var _ = bundle;

            var sharedTx = ATransaction(0);
            await SealOrphanedBlockAsync(bundle, 11, new List<ISignedTransaction> { sharedTx });

            await reconciler.ReconcileAsync(bundle, commonAncestor: 10, orphanedHead: 11,
                winningBranchTransactions: new List<ISignedTransaction> { sharedTx }, CancellationToken.None);

            var pending = await relay.GetPendingAsync(10);
            Assert.DoesNotContain(pending, tx => tx.Hash.ToHex() == sharedTx.Hash.ToHex());
        }

        [Fact]
        public async Task Given_AReorgFromBranchXToBranchY_When_ATransactionIsOnlyInTheWinningBranch_Then_ItIsRemovedFromThePool()
        {
            var (reconciler, txPool, relay, bundle) = await HarnessAsync();
            using var _ = bundle;

            var winningOnlyTx = ATransaction(0);
            await txPool.AddAsync(winningOnlyTx);
            Assert.Single(await relay.GetPendingAsync(10));

            await reconciler.ReconcileAsync(bundle, commonAncestor: 10, orphanedHead: 10,
                winningBranchTransactions: new List<ISignedTransaction> { winningOnlyTx }, CancellationToken.None);

            Assert.Empty(await relay.GetPendingAsync(10));
        }

        [Fact]
        public async Task Given_AReorgThatOnlyRewindsAndNeverReconciles_When_OrphanedTxsExisted_Then_TheyAreLostFromThePool()
        {
            var (_, txPool, relay, bundle) = await HarnessAsync();
            using var _ = bundle;

            var orphanedOnlyTx = ATransaction(0);
            await SealOrphanedBlockAsync(bundle, 11, new List<ISignedTransaction> { orphanedOnlyTx });

            var pending = await relay.GetPendingAsync(10);
            Assert.DoesNotContain(pending, tx => tx.Hash.ToHex() == orphanedOnlyTx.Hash.ToHex());
        }
    }
}
