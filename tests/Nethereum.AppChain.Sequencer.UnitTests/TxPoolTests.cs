using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class TxPoolTests
    {
        [Fact]
        public async Task TxPool_AddTransaction_ReturnsTxHash()
        {
            var pool = new TxPool();
            var tx = CreateSignedTransaction();

            var hash = await pool.AddAsync(tx);

            Assert.NotNull(hash);
            Assert.Equal(32, hash.Length);
            Assert.Equal(1, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_AddMultipleTransactions_AllAdded()
        {
            var pool = new TxPool();

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await pool.AddAsync(tx);
            }

            Assert.Equal(5, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_GetPending_ReturnsRequestedCount()
        {
            var pool = new TxPool();

            for (int i = 0; i < 10; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await pool.AddAsync(tx);
            }

            var pending = await pool.GetPendingAsync(5);

            Assert.Equal(5, pending.Count);
            Assert.Equal(10, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_GetPending_ReturnsAllWhenLessThanRequested()
        {
            var pool = new TxPool();

            for (int i = 0; i < 3; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await pool.AddAsync(tx);
            }

            var pending = await pool.GetPendingAsync(100);

            Assert.Equal(3, pending.Count);
        }

        [Fact]
        public async Task TxPool_Remove_DecreasesPendingCount()
        {
            var pool = new TxPool();
            var tx = CreateSignedTransaction();

            var hash = await pool.AddAsync(tx);
            Assert.Equal(1, pool.PendingCount);

            await pool.RemoveAsync(hash);
            Assert.Equal(0, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_Remove_NonExistent_DoesNotThrow()
        {
            var pool = new TxPool();
            var fakeHash = new byte[32];

            await pool.RemoveAsync(fakeHash);

            Assert.Equal(0, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_Clear_RemovesAllTransactions()
        {
            var pool = new TxPool();

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await pool.AddAsync(tx);
            }

            Assert.Equal(5, pool.PendingCount);

            await pool.ClearAsync();

            Assert.Equal(0, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_MaxPoolSize_ThrowsWhenFull()
        {
            var pool = new TxPool(maxPoolSize: 3);

            for (int i = 0; i < 3; i++)
            {
                var hash = await pool.AddAsync(CreateSignedTransaction(nonce: i));
                Assert.NotNull(hash);
            }

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.AddAsync(CreateSignedTransaction(nonce: 3)));
            Assert.Equal(3, pool.PendingCount);
        }

        [Fact]
        public async Task TxPool_GetPendingNonce_ReturnsConfirmedWhenNoPending()
        {
            var pool = new TxPool();
            var confirmedNonce = new BigInteger(5);

            var result = await pool.GetPendingNonceAsync("0xSender1", confirmedNonce);

            Assert.Equal(confirmedNonce, result);
        }

        [Fact]
        public async Task TxPool_GetPendingNonce_ReturnsPendingWhenHigherThanConfirmed()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(5));

            var result = await pool.GetPendingNonceAsync("0xSender1", BigInteger.Zero);

            Assert.Equal(new BigInteger(6), result);
        }

        [Fact]
        public async Task TxPool_GetPendingNonce_ReturnsConfirmedWhenHigherThanPending()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(2));

            var result = await pool.GetPendingNonceAsync("0xSender1", new BigInteger(10));

            Assert.Equal(new BigInteger(10), result);
        }

        [Fact]
        public async Task TxPool_TrackPendingNonce_TracksHighestNoncePlusOne()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(0));
            pool.TrackPendingNonce("0xSender1", new BigInteger(1));
            pool.TrackPendingNonce("0xSender1", new BigInteger(2));

            var result = await pool.GetPendingNonceAsync("0xSender1", BigInteger.Zero);
            Assert.Equal(new BigInteger(3), result);
        }

        [Fact]
        public async Task TxPool_TrackPendingNonce_DoesNotDecrease()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(5));
            pool.TrackPendingNonce("0xSender1", new BigInteger(2));

            var result = await pool.GetPendingNonceAsync("0xSender1", BigInteger.Zero);
            Assert.Equal(new BigInteger(6), result);
        }

        [Fact]
        public async Task TxPool_GetPendingNonce_CaseInsensitiveAddress()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xABCD", new BigInteger(3));

            var result = await pool.GetPendingNonceAsync("0xabcd", BigInteger.Zero);
            Assert.Equal(new BigInteger(4), result);
        }

        [Fact]
        public async Task TxPool_ResetPendingNonces_ClearsBoth()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(5));
            pool.IncrementSenderTxCount("0xSender1");

            pool.ResetPendingNonces();

            var nonce = await pool.GetPendingNonceAsync("0xSender1", BigInteger.Zero);
            Assert.Equal(BigInteger.Zero, nonce);
            Assert.Equal(0, pool.GetSenderTxCount("0xSender1"));
        }

        [Fact]
        public void TxPool_IncrementSenderTxCount_Increments()
        {
            var pool = new TxPool();

            Assert.Equal(0, pool.GetSenderTxCount("0xSender1"));

            pool.IncrementSenderTxCount("0xSender1");
            Assert.Equal(1, pool.GetSenderTxCount("0xSender1"));

            pool.IncrementSenderTxCount("0xSender1");
            Assert.Equal(2, pool.GetSenderTxCount("0xSender1"));
        }

        [Fact]
        public void TxPool_SenderTxCount_IsolatedPerSender()
        {
            var pool = new TxPool();

            pool.IncrementSenderTxCount("0xSender1");
            pool.IncrementSenderTxCount("0xSender1");
            pool.IncrementSenderTxCount("0xSender2");

            Assert.Equal(2, pool.GetSenderTxCount("0xSender1"));
            Assert.Equal(1, pool.GetSenderTxCount("0xSender2"));
            Assert.Equal(0, pool.GetSenderTxCount("0xSender3"));
        }

        [Fact]
        public void TxPool_MaxTxsPerSender_ReturnsConfiguredValue()
        {
            var pool = new TxPool(maxTxsPerSender: 500);
            Assert.Equal(500, pool.MaxTxsPerSender);
        }

        [Fact]
        public async Task TxPool_Clear_AlsoClearsPendingNoncesAndSenderCounts()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xSender1", new BigInteger(5));
            pool.IncrementSenderTxCount("0xSender1");
            await pool.AddAsync(CreateSignedTransaction());

            await pool.ClearAsync();

            Assert.Equal(0, pool.PendingCount);
            var nonce = await pool.GetPendingNonceAsync("0xSender1", BigInteger.Zero);
            Assert.Equal(BigInteger.Zero, nonce);
            Assert.Equal(0, pool.GetSenderTxCount("0xSender1"));
        }

        [Fact]
        public async Task TxPool_MultipleSenders_IndependentPendingNonces()
        {
            var pool = new TxPool();

            pool.TrackPendingNonce("0xAlice", new BigInteger(0));
            pool.TrackPendingNonce("0xAlice", new BigInteger(1));
            pool.TrackPendingNonce("0xBob", new BigInteger(10));

            var aliceNonce = await pool.GetPendingNonceAsync("0xAlice", BigInteger.Zero);
            var bobNonce = await pool.GetPendingNonceAsync("0xBob", BigInteger.Zero);

            Assert.Equal(new BigInteger(2), aliceNonce);
            Assert.Equal(new BigInteger(11), bobNonce);
        }

        private ISignedTransaction CreateSignedTransaction(int nonce = 0)
        {
            var privateKey = new EthECKey("0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f");

            var transaction = new Transaction1559(
                chainId: new BigInteger(1),
                nonce: new BigInteger(nonce),
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: BigInteger.Zero,
                data: null,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }
}
