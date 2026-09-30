using System;
using System.IO;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionEnableLogIndexGuardTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionlogindexguard_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public void PromotionEnabled_WithEnableLogIndex_ThrowsAtOpen()
        {
            var dir = Path.Combine(_root, "guard");
            var opts = new RocksDbStorageOptions { PromotionEnabled = true, EnableLogIndex = true };

            var ex = Assert.Throws<NotSupportedException>(() =>
                RocksDbChainStoreBundle.Open(dir, storageOptions: opts));
            Assert.Contains("Promotion", ex.Message);
            Assert.Contains("EnableLogIndex", ex.Message);
        }

        [Fact]
        public void PromotionEnabled_WithEnableLogIndexOff_DoesNotThrow()
        {
            var dir = Path.Combine(_root, "promotion-only");
            var opts = new RocksDbStorageOptions { PromotionEnabled = true };

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: opts);
            Assert.NotNull(bundle);
        }

        [Fact]
        public void EnableLogIndex_WithPromotionOff_DoesNotThrow()
        {
            var dir = Path.Combine(_root, "logindex-only");
            var opts = new RocksDbStorageOptions { EnableLogIndex = true };

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: opts);
            Assert.NotNull(bundle);
        }
    }
}
