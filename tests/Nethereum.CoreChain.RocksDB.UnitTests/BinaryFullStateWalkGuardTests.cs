using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// Guards the post-R1/R2 contract that Patricia-shape persistent stores
    /// (RocksDbStateStore) do not implement <see cref="IRawStorageEnumerator"/>
    /// because they keccak-hash slots before persisting (Yellow Paper §4.1 /
    /// EIP-2364). EIP-7864 Binary trie key derivation needs the raw slot, so
    /// <see cref="BinaryIncrementalStateRootCalculator"/> must surface a clear
    /// error rather than silently producing a wrong root.
    /// </summary>
    public class BinaryFullStateWalkGuardTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _state;

        public BinaryFullStateWalkGuardTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_binary_guard_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _state = new RocksDbStateStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public void RocksDbStateStore_DoesNotImplement_IRawStorageEnumerator()
        {
            Assert.False(_state is IRawStorageEnumerator,
                "Patricia-shape persistent stores keccak-hash slots; raw slot is " +
                "not reconstructable, so IRawStorageEnumerator must NOT be implemented.");
        }

        [Fact]
        public async Task BinaryCalculator_FullStateWalk_OnRocksDb_WithStorage_ThrowsNotSupportedWithClearMessage()
        {
            var address = "0x1111111111111111111111111111111111111111";
            await _state.SaveAccountAsync(address, new Account { Balance = 1000, Nonce = 1 });
            await _state.SaveStorageAsync(address, System.Numerics.BigInteger.One, new byte[] { 0x42 });

            var calc = new BinaryIncrementalStateRootCalculator(_state);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => calc.ComputeFullStateRootAsync());

            Assert.Contains("IRawStorageEnumerator", ex.Message);
            Assert.Contains("EIP-7864", ex.Message);
            Assert.Contains("Yellow Paper", ex.Message);
        }

        [Fact]
        public async Task BinaryCalculator_FullStateWalk_OnRocksDb_NoStorage_SucceedsSilently()
        {
            await _state.SaveAccountAsync(
                "0x2222222222222222222222222222222222222222",
                new Account { Balance = 500, Nonce = 0 });

            var calc = new BinaryIncrementalStateRootCalculator(_state);
            var root = await calc.ComputeFullStateRootAsync();

            Assert.NotNull(root);
            Assert.Equal(32, root.Length);
        }
    }
}
