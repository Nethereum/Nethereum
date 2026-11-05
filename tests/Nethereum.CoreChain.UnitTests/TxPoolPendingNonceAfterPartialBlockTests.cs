using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class TxPoolPendingNonceAfterPartialBlockTests
    {
        private const string SenderPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";
        private const string Recipient = "0x3535353535353535353535353535353535353535";

        private static ISignedTransaction Transfer(int nonce) =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                SenderPrivateKey.HexToByteArray(), new BigInteger(1), Recipient, 1, nonce, 1_000_000_000, 21_000, ""));

        [Fact]
        public async Task Given_TwoPendingTransfers_When_ABlockIncludesOnlyTheFirstAndTheNoncesAreReset_Then_ThePendingNonceStillCoversTheSecond()
        {
            var pool = new TxPool();
            var sender = new EthECKey(SenderPrivateKey).GetPublicAddress();
            var first = Transfer(0);
            var second = Transfer(1);
            await pool.AddAsync(first);
            await pool.AddAsync(second);
            pool.TrackPendingNonce(sender, 0);
            pool.TrackPendingNonce(sender, 1);

            await pool.RemoveAsync(first.Hash);
            pool.ResetPendingNonces();

            Assert.Equal(new BigInteger(2), await pool.GetPendingNonceAsync(sender, confirmedNonce: 1));
            Assert.Equal(1, pool.GetSenderTxCount(sender));
        }

        [Fact]
        public async Task Given_APendingTransfer_When_ABlockIncludesItAndTheNoncesAreReset_Then_ThePendingNonceIsTheConfirmedNonce()
        {
            var pool = new TxPool();
            var sender = new EthECKey(SenderPrivateKey).GetPublicAddress();
            var only = Transfer(0);
            await pool.AddAsync(only);
            pool.TrackPendingNonce(sender, 0);

            await pool.RemoveAsync(only.Hash);
            pool.ResetPendingNonces();

            Assert.Equal(BigInteger.One, await pool.GetPendingNonceAsync(sender, confirmedNonce: 1));
            Assert.Equal(0, pool.GetSenderTxCount(sender));
        }
    }
}
