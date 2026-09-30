using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class HistoryStoreProfilesTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"histprofiles_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public void Opens_With_Catalogue_And_RangeScans_In_NumericOrder()
        {
            Directory.CreateDirectory(_dir);
            var cache = RocksProfiles.CreateSharedCache(64 * 1024 * 1024);
            var dbOptions = RocksProfiles.Db(cores: 4, sharedMemtableBudgetBytes: 128 * 1024 * 1024);
            var cfs = RocksProfiles.BuildColumnFamilies(cache);

            using var db = RocksDb.Open(dbOptions, _dir, cfs);
            var txBody = db.GetColumnFamily(HistoryColumnFamilies.TxBody);

            db.Put(HistoryKeys.TxKey(6, 0), Val("6-0"), txBody);
            db.Put(HistoryKeys.TxKey(5, 1), Val("5-1"), txBody);
            db.Put(HistoryKeys.TxKey(5, 0), Val("5-0"), txBody);
            db.Put(HistoryKeys.TxKey(5, uint.MaxValue), Val("5-max"), txBody);

            var seen = new List<(ulong n, uint i)>();
            using (var it = db.NewIterator(txBody))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                    seen.Add((HistoryKeys.ReadBlockNumber(it.Key()), HistoryKeys.ReadTxIndex(it.Key())));
            }
            Assert.Equal(new[] { (5UL, 0u), (5UL, 1u), (5UL, uint.MaxValue), (6UL, 0u) }, seen);

            Assert.Equal("5-1", Str(db.Get(HistoryKeys.TxKey(5, 1), txBody)));

            var block5 = new List<uint>();
            using (var it = db.NewIterator(txBody))
            {
                for (it.Seek(HistoryKeys.TxKey(5, 0)); it.Valid(); it.Next())
                {
                    var key = it.Key();
                    if (HistoryKeys.ReadBlockNumber(key) != 5) break;
                    block5.Add(HistoryKeys.ReadTxIndex(key));
                }
            }
            Assert.Equal(new[] { 0u, 1u, uint.MaxValue }, block5);
        }

        private static byte[] Val(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        private static string Str(byte[] b) => b == null ? null : System.Text.Encoding.UTF8.GetString(b);
    }
}
