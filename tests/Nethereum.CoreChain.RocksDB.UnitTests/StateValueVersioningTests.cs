using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateValueVersioningTests : IDisposable
    {
        private const string Addr = "0x0000000000000000000000000000000000000001";
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public StateValueVersioningTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-stateval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
        }

        private static byte[] AccountKey(string addr) =>
            new Sha3Keccack().CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(addr).HexToByteArray());

        [Fact]
        public async Task Flat_Account_RoundTrips_Through_Versioned_Envelope()
        {
            var store = new RocksDbStateStore(_mgr, stateValuesVersioned: true);
            await store.SaveAccountAsync(Addr, new Account { Nonce = 3, Balance = 9 });

            var raw = _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, AccountKey(Addr));
            Assert.Equal(StateValueEnvelope.CurrentVersion, raw[0]);

            var acct = await store.GetAccountAsync(Addr);
            Assert.Equal((EvmUInt256)3, acct.Nonce);
            Assert.Equal((EvmUInt256)9, acct.Balance);
        }

        [Fact]
        public async Task Flat_Account_Default_Is_Unversioned_And_Works()
        {
            var store = new RocksDbStateStore(_mgr);
            await store.SaveAccountAsync(Addr, new Account { Nonce = 3, Balance = 9 });

            var raw = _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, AccountKey(Addr));
            Assert.NotEqual(StateValueEnvelope.CurrentVersion, raw[0]);

            var acct = await store.GetAccountAsync(Addr);
            Assert.Equal((EvmUInt256)3, acct.Nonce);
        }

        [Fact]
        public void Consensus_Trie_Leaf_Is_Not_Versioned()
        {
            var leaf = AccountEncoder.Current.Encode(new Account
            {
                Nonce = 3,
                Balance = 9,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            });
            Assert.NotEqual(StateValueEnvelope.CurrentVersion, leaf[0]);
        }

        [Fact]
        public async Task History_Entry_RoundTrips_Through_Versioned_Envelope()
        {
            var diffStore = new RocksDbStateDiffStore(_mgr, stateValuesVersioned: true);
            var blockDiff = new BlockStateDiff { BlockNumber = 5 };
            blockDiff.AccountDiffs.Add(new AccountDiffEntry
            {
                Address = Addr,
                PreValue = new Account { Nonce = 2, Balance = 7 }
            });
            await diffStore.SaveBlockDiffAsync(blockDiff);

            var (found, pre) = await diffStore.GetFirstAccountPreValueAfterBlockAsync(Addr, 4);
            Assert.True(found);
            Assert.NotNull(pre);
            Assert.Equal((EvmUInt256)2, pre.Nonce);
            Assert.Equal((EvmUInt256)7, pre.Balance);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
