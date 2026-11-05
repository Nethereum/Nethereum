using System;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Snapshots;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbStateSnapshotAddressTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"snapshot_address_{Guid.NewGuid():N}");

        public RocksDbStateSnapshotAddressTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public void Given_an_account_modification_tracked_by_EvmAddress_When_it_is_tracked_again_by_its_hex_string_Then_both_land_on_the_same_snapshot_entry()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var snapshot = new RocksDbStateSnapshot(manager, 1);
            var address = EvmAddress.FromHex("0x00000000000000000000000000000000000000aa");

            snapshot.TrackAccountModification(address);
            snapshot.TrackAccountModification(address.ToHexLower());

            var tracked = Assert.Single(snapshot.ModifiedAddresses);
            Assert.Equal(Nethereum.CoreChain.Storage.StateKeys.AccountKeyHex(address), tracked);
        }
    }
}
