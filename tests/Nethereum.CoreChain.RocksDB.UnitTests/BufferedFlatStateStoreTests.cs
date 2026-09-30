using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BufferedFlatStateStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbStateStore _inner;
        private readonly BufferedFlatStateStore _buffer;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public BufferedFlatStateStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-bufflat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _inner = new RocksDbStateStore(_mgr);
            _buffer = new BufferedFlatStateStore(_inner);
        }

        public void Dispose()
        {
            try { _mgr.Dispose(); } catch { }
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Buffering_ReadsOwnWrites_BeforeFlush_AndNothingReachesInner()
        {
            _buffer.BeginBuffering();
            await _buffer.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 1 });
            await _buffer.SaveStorageAsync(AddrA, BigInteger.One, new byte[] { 0x42 });

            var acc = await _buffer.GetAccountAsync(AddrA);
            Assert.NotNull(acc);
            Assert.Equal((EvmUInt256)100, acc.Balance);
            Assert.Equal(new byte[] { 0x42 }, await _buffer.GetStorageAsync(AddrA, BigInteger.One));

            Assert.Null(await _inner.GetAccountAsync(AddrA));
            Assert.Null(await _inner.GetStorageAsync(AddrA, BigInteger.One));

            await _buffer.FlushBufferAsync();

            Assert.NotNull(await _inner.GetAccountAsync(AddrA));
            Assert.Equal(new byte[] { 0x42 }, await _inner.GetStorageAsync(AddrA, BigInteger.One));
        }

        [Fact]
        public async Task Buffering_LeadingZeroValue_ReadFromOverlay_IsAlreadyTrimmed_BeforeFlush()
        {
            _buffer.BeginBuffering();
            var padded = new byte[32];
            padded[31] = 0x09;
            await _buffer.SaveStorageAsync(AddrA, BigInteger.One, padded);

            var fromOverlay = await _buffer.GetStorageAsync(AddrA, BigInteger.One);
            Assert.Equal(new byte[] { 0x09 }, fromOverlay);

            await _buffer.FlushBufferAsync();

            var fromDisk = await _inner.GetStorageAsync(AddrA, BigInteger.One);
            Assert.Equal(fromOverlay, fromDisk);
        }

        [Fact]
        public async Task Buffering_DeleteThenRead_ReturnsNull_BeforeAndAfterFlush()
        {
            await _inner.SaveAccountAsync(AddrA, new Account { Balance = 10, Nonce = 1 });

            _buffer.BeginBuffering();
            await _buffer.DeleteAccountAsync(AddrA);
            Assert.Null(await _buffer.GetAccountAsync(AddrA));

            await _buffer.FlushBufferAsync();
            Assert.Null(await _buffer.GetAccountAsync(AddrA));
            Assert.Null(await _inner.GetAccountAsync(AddrA));
        }

        [Fact]
        public async Task Buffering_StorageClearThenWrite_OnlyNewSlotSurvives_BeforeAndAfterFlush()
        {
            await _inner.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await _inner.SaveStorageAsync(AddrA, 2, new byte[] { 0x02 });

            _buffer.BeginBuffering();
            await _buffer.ClearStorageAsync(AddrA);
            await _buffer.SaveStorageAsync(AddrA, 3, new byte[] { 0x03 });

            Assert.Null(await _buffer.GetStorageAsync(AddrA, 1));
            Assert.Null(await _buffer.GetStorageAsync(AddrA, 2));
            Assert.Equal(new byte[] { 0x03 }, await _buffer.GetStorageAsync(AddrA, 3));

            await _buffer.FlushBufferAsync();

            Assert.Null(await _inner.GetStorageAsync(AddrA, 1));
            Assert.Null(await _inner.GetStorageAsync(AddrA, 2));
            Assert.Equal(new byte[] { 0x03 }, await _inner.GetStorageAsync(AddrA, 3));
        }

        [Fact]
        public async Task Buffering_DeleteThenRecreateInBlock_FinalValueWins()
        {
            await _inner.SaveAccountAsync(AddrA, new Account { Balance = 1, Nonce = 1 });

            _buffer.BeginBuffering();
            await _buffer.DeleteAccountAsync(AddrA);
            await _buffer.SaveAccountAsync(AddrA, new Account { Balance = 999, Nonce = 5 });

            var mid = await _buffer.GetAccountAsync(AddrA);
            Assert.NotNull(mid);
            Assert.Equal((EvmUInt256)999, mid.Balance);

            await _buffer.FlushBufferAsync();

            var final = await _inner.GetAccountAsync(AddrA);
            Assert.NotNull(final);
            Assert.Equal((EvmUInt256)999, final.Balance);
            Assert.Equal((EvmUInt256)5, final.Nonce);
        }

        [Fact]
        public async Task DiscardBuffer_DropsOverlay_InnerUntouched()
        {
            await _inner.SaveAccountAsync(AddrA, new Account { Balance = 5, Nonce = 1 });

            _buffer.BeginBuffering();
            await _buffer.SaveAccountAsync(AddrA, new Account { Balance = 999, Nonce = 9 });
            await _buffer.SaveAccountAsync(AddrB, new Account { Balance = 1, Nonce = 1 });

            await _buffer.DiscardBufferAsync();

            var a = await _inner.GetAccountAsync(AddrA);
            Assert.Equal((EvmUInt256)5, a.Balance);
            Assert.Null(await _inner.GetAccountAsync(AddrB));
        }

        [Fact]
        public async Task PassThrough_WhenNeverBuffering_WritesReachInnerImmediately()
        {
            await _buffer.SaveAccountAsync(AddrA, new Account { Balance = 42, Nonce = 1 });
            await _buffer.SaveStorageAsync(AddrA, 7, new byte[] { 0x07 });

            var inner = await _inner.GetAccountAsync(AddrA);
            Assert.NotNull(inner);
            Assert.Equal((EvmUInt256)42, inner.Balance);
            Assert.Equal(new byte[] { 0x07 }, await _inner.GetStorageAsync(AddrA, 7));
        }

        [Fact]
        public async Task PassThrough_DeleteAndClear_ReachInnerImmediately()
        {
            await _inner.SaveAccountAsync(AddrA, new Account { Balance = 1, Nonce = 1 });
            await _inner.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });

            await _buffer.ClearStorageAsync(AddrA);
            await _buffer.DeleteAccountAsync(AddrA);

            Assert.Null(await _inner.GetAccountAsync(AddrA));
            Assert.Null(await _inner.GetStorageAsync(AddrA, 1));
        }

        [Fact]
        public async Task ByHashWrites_AlwaysUnbuffered_VisibleImmediately_EvenWhileBuffering()
        {
            _buffer.BeginBuffering();
            var accountHash = new byte[32];
            accountHash[0] = 0xAB;
            await _buffer.SaveAccountByHashAsync(accountHash, new Account { Balance = 7, Nonce = 1 });

            using (var it = _mgr.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS))
            {
                it.Seek(accountHash);
                Assert.True(it.Valid());
                Assert.Equal(accountHash, it.Key());
            }

            await _buffer.DiscardBufferAsync();
            using (var it = _mgr.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS))
            {
                it.Seek(accountHash);
                Assert.True(it.Valid());
                Assert.Equal(accountHash, it.Key());
            }
        }
    }
}
