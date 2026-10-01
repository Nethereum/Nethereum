using System;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.Merkle.Patricia.Storage;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbManagerFlushWriteStallTests
    {
        private const string LiveWriteStallMessage =
            "Operation failed. Try again.: Writes have been stopped, thus unable to perform manual flush. Please try again later";

        [Theory]
        [InlineData(LiveWriteStallMessage)]
        [InlineData("Writes have been stopped, thus unable to perform manual flush. Please try again later")]
        [InlineData("writes have been stopped")]
        [InlineData("Unable to perform manual flush right now")]
        [InlineData("Operation failed. Please try again later")]
        public void IsTransientFlushStall_RecognisesWriteStall_AsTransient(string message)
        {
            Assert.True(RocksDbManager.IsTransientFlushStall(message));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Corruption: block checksum mismatch")]
        [InlineData("IO error: No space left on device")]
        [InlineData("Invalid argument: column family not found")]
        public void IsTransientFlushStall_RejectsRealErrorsAndEmpty_AsNonTransient(string message)
        {
            Assert.False(RocksDbManager.IsTransientFlushStall(message));
        }

        [Fact]
        public void Flush_OnHealthyDatabase_DoesNotThrow()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_flush_stall_{Guid.NewGuid():N}");
            try
            {
                using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                var ex = Record.Exception(() => manager.Flush());
                Assert.Null(ex);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                {
                    try { Directory.Delete(dbPath, true); } catch { }
                }
            }
        }
    }
}
