using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainSenderRecoveryTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        private sealed class ThreadRecordingVerifier : ITransactionVerificationAndRecovery
        {
            private readonly ITransactionVerificationAndRecovery _inner = new TransactionVerificationAndRecoveryImp();
            private readonly ConcurrentBag<string> _threads = new ConcurrentBag<string>();

            public string[] RecoveryThreads => _threads.ToArray();

            public byte[] GetPublicKey(ISignedTransaction transaction) => _inner.GetPublicKey(transaction);
            public byte[] GetPublicKey(string rlp) => _inner.GetPublicKey(rlp);
            public string GetSenderAddress(string rlp) => _inner.GetSenderAddress(rlp);
            public bool VerifyTransaction(ISignedTransaction transaction) => _inner.VerifyTransaction(transaction);
            public bool VerifyTransaction(string rlp) => _inner.VerifyTransaction(rlp);

            public string GetSenderAddress(ISignedTransaction transaction)
            {
                _threads.Add(Thread.CurrentThread.Name ?? "");
                return _inner.GetSenderAddress(transaction);
            }
        }

        private static ISignedTransaction Transfer(DevChainConfig config, int nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), config.ChainId, RecipientAddress, 100, nonce, 2_000_000_000, 21_000, ""));

        [Fact]
        public async Task Given_ADevChainBlockManagerWithTransfers_When_TheBlockIsMined_Then_EverySenderIsRecoveredOnceOnASenderRecoveryThread()
        {
            const int transfers = 12;
            var config = DevChainConfig.Default;
            using var bundle = InMemoryChainStoreBundle.Open();
            var verifier = new ThreadRecordingVerifier();
            var processor = new TransactionProcessor(bundle.State, bundle.Blocks, config, verifier, config.GetHardforkConfig());
            using var manager = new BlockManager(
                bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, processor, verifier, config);
            await manager.InitializeAsync();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account { Balance = BigInteger.Pow(10, 21), Nonce = 0 });
            foreach (var nonce in Enumerable.Range(0, transfers)) manager.AddPendingTransaction(Transfer(config, nonce));

            await manager.MineBlockAsync();

            Assert.Equal(transfers, verifier.RecoveryThreads.Length);
            Assert.All(verifier.RecoveryThreads, name => Assert.StartsWith("sender-recovery-", name));
        }
    }
}
