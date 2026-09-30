using System;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbManagerDisposalGuardTests : IDisposable
    {
        private readonly string _dir = Path.Combine(
            Path.GetTempPath(), "neth-dispose-guard-" + Guid.NewGuid().ToString("N"));

        private RocksDbManager OpenAndDispose()
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            manager.Dispose();
            return manager;
        }

        [Fact]
        public void Given_ADisposedManager_When_GetIsCalled_Then_ItThrowsObjectDisposedRatherThanKillingTheProcess()
        {
            var manager = OpenAndDispose();
            Assert.Throws<ObjectDisposedException>(
                () => manager.Get(RocksDbManager.CF_METADATA, new byte[] { 1 }));
        }

        [Fact]
        public void Given_ADisposedManager_When_AnyNativeMemberIsCalled_Then_EachThrowsObjectDisposed()
        {
            var manager = OpenAndDispose();

            Assert.Throws<ObjectDisposedException>(
                () => manager.Get(RocksDbManager.CF_METADATA, new byte[] { 1 }));
            Assert.Throws<ObjectDisposedException>(
                () => manager.KeyExists(RocksDbManager.CF_METADATA, new byte[] { 1 }));
            Assert.Throws<ObjectDisposedException>(
                () => manager.Put(RocksDbManager.CF_METADATA, new byte[] { 1 }, new byte[] { 2 }));
            Assert.Throws<ObjectDisposedException>(
                () => manager.Delete(RocksDbManager.CF_METADATA, new byte[] { 1 }));
            Assert.Throws<ObjectDisposedException>(
                () => manager.CreateIterator(RocksDbManager.CF_METADATA));
            Assert.Throws<ObjectDisposedException>(() => manager.CreateSnapshot());
            Assert.Throws<ObjectDisposedException>(() => manager.Flush());
        }

        [Fact]
        public void Given_ALiveManager_When_TheSameMembersAreCalled_Then_TheyServeNormally()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });

            manager.Put(RocksDbManager.CF_METADATA, new byte[] { 1 }, new byte[] { 2 });
            Assert.Equal(new byte[] { 2 }, manager.Get(RocksDbManager.CF_METADATA, new byte[] { 1 }));
            Assert.True(manager.KeyExists(RocksDbManager.CF_METADATA, new byte[] { 1 }));

            using (var iterator = manager.CreateIterator(RocksDbManager.CF_METADATA))
                Assert.NotNull(iterator);

            manager.Flush();
            manager.Delete(RocksDbManager.CF_METADATA, new byte[] { 1 });
            Assert.False(manager.KeyExists(RocksDbManager.CF_METADATA, new byte[] { 1 }));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
