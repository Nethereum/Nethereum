using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class SqliteStateStoreSnapshotTests : System.IDisposable
    {
        private readonly SqliteStorageManager _manager;
        private readonly SqliteStateStore _store;

        public SqliteStateStoreSnapshotTests()
        {
            _manager = new SqliteStorageManager(null, deleteOnDispose: true);
            _store = new SqliteStateStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresAccountBalance()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100, Nonce = 1 });

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveAccountAsync("0x1234", new Account { Balance = 500, Nonce = 5 });
            var afterChange = await _store.GetAccountAsync("0x1234");
            Assert.Equal(500, afterChange.Balance);

            await _store.RevertSnapshotAsync(snapshot);
            var afterRevert = await _store.GetAccountAsync("0x1234");
            Assert.Equal(100, afterRevert.Balance);
            Assert.Equal(1, afterRevert.Nonce);
        }

        [Fact]
        public async Task Snapshot_Commit_KeepsModifiedState()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveAccountAsync("0x1234", new Account { Balance = 500 });

            await _store.CommitSnapshotAsync(snapshot);

            var afterCommit = await _store.GetAccountAsync("0x1234");
            Assert.Equal(500, afterCommit.Balance);
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresStorage()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });
            await _store.SaveStorageAsync("0x1234", BigInteger.One, new byte[] { 0x01, 0x02 });

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveStorageAsync("0x1234", BigInteger.One, new byte[] { 0xFF, 0xFE });
            var afterChange = await _store.GetStorageAsync("0x1234", BigInteger.One);
            Assert.Equal(new byte[] { 0xFF, 0xFE }, afterChange);

            await _store.RevertSnapshotAsync(snapshot);
            var afterRevert = await _store.GetStorageAsync("0x1234", BigInteger.One);
            Assert.Equal(new byte[] { 0x01, 0x02 }, afterRevert);
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresDeletedAccount()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });
            Assert.True(await _store.AccountExistsAsync("0x1234"));

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.DeleteAccountAsync("0x1234");
            Assert.False(await _store.AccountExistsAsync("0x1234"));

            await _store.RevertSnapshotAsync(snapshot);
            Assert.True(await _store.AccountExistsAsync("0x1234"));
            var afterRevert = await _store.GetAccountAsync("0x1234");
            Assert.Equal(100, afterRevert.Balance);
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresClearedStorage()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });
            await _store.SaveStorageAsync("0x1234", BigInteger.One, new byte[] { 0x01 });
            await _store.SaveStorageAsync("0x1234", new BigInteger(2), new byte[] { 0x02 });

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.ClearStorageAsync("0x1234");
            var afterClear = await _store.GetAllStorageAsync("0x1234");
            Assert.Empty(afterClear);

            await _store.RevertSnapshotAsync(snapshot);
            var afterRevert = await _store.GetAllStorageAsync("0x1234");
            Assert.Equal(2, afterRevert.Count);
            Assert.Equal(new byte[] { 0x01 }, afterRevert[BigInteger.One]);
            Assert.Equal(new byte[] { 0x02 }, afterRevert[new BigInteger(2)]);
        }

        [Fact]
        public async Task Snapshot_Nested_InnerRevertOuterCommit()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });

            var snap1 = await _store.CreateSnapshotAsync();
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 200 });

            var snap2 = await _store.CreateSnapshotAsync();
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 300 });

            var afterInner = await _store.GetAccountAsync("0x1234");
            Assert.Equal(300, afterInner.Balance);

            await _store.RevertSnapshotAsync(snap2);
            var afterInnerRevert = await _store.GetAccountAsync("0x1234");
            Assert.Equal(200, afterInnerRevert.Balance);

            await _store.CommitSnapshotAsync(snap1);
            var afterOuterCommit = await _store.GetAccountAsync("0x1234");
            Assert.Equal(200, afterOuterCommit.Balance);
        }

        [Fact]
        public async Task Snapshot_DirtyTracking_RestoredOnRevert()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });
            await _store.ClearDirtyTrackingAsync();

            await _store.SaveAccountAsync("0xAAAA", new Account { Balance = 50 });
            var dirtyBefore = await _store.GetDirtyAccountAddressesAsync();
            Assert.Contains(dirtyBefore, a => a.Contains("aaaa"));

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveAccountAsync("0xBBBB", new Account { Balance = 75 });
            var dirtyDuring = await _store.GetDirtyAccountAddressesAsync();
            Assert.Contains(dirtyDuring, a => a.Contains("bbbb"));

            await _store.RevertSnapshotAsync(snapshot);
            var dirtyAfter = await _store.GetDirtyAccountAddressesAsync();
            Assert.Contains(dirtyAfter, a => a.Contains("aaaa"));
            Assert.DoesNotContain(dirtyAfter, a => a.Contains("bbbb"));
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresNewAccountCreation()
        {
            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveAccountAsync("0x5678", new Account { Balance = 999 });
            Assert.True(await _store.AccountExistsAsync("0x5678"));

            await _store.RevertSnapshotAsync(snapshot);
            Assert.False(await _store.AccountExistsAsync("0x5678"));
        }

        [Fact]
        public async Task Snapshot_Revert_RestoresNewStorageSlot()
        {
            await _store.SaveAccountAsync("0x1234", new Account { Balance = 100 });

            var snapshot = await _store.CreateSnapshotAsync();

            await _store.SaveStorageAsync("0x1234", new BigInteger(42), new byte[] { 0xAB });
            var afterAdd = await _store.GetStorageAsync("0x1234", new BigInteger(42));
            Assert.NotNull(afterAdd);

            await _store.RevertSnapshotAsync(snapshot);
            var afterRevert = await _store.GetStorageAsync("0x1234", new BigInteger(42));
            Assert.Null(afterRevert);
        }
    }
}
