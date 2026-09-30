using System;
using System.Text;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    public sealed class FreezerHistoryIndexProgress : IFreezerIndexProgress
    {
        private static readonly byte[] ByHashCursorKey = Encoding.ASCII.GetBytes("freezer_byhash_reindex_head");
        private static readonly byte[] LvProgressKey = Encoding.ASCII.GetBytes("filtermaps_lv_progress");

        private readonly RocksDbManager _rocks;
        private readonly string _controlColumnFamily;

        public FreezerHistoryIndexProgress(RocksDbManager rocks, string controlColumnFamily)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _controlColumnFamily = controlColumnFamily ?? throw new ArgumentNullException(nameof(controlColumnFamily));
        }

        public ulong GetByHashCursor()
        {
            var raw = _rocks.Get(_controlColumnFamily, ByHashCursorKey);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public void SetByHashCursor(ulong itemNumber)
            => _rocks.Put(_controlColumnFamily, ByHashCursorKey, RocksDbManager.Write64BE(itemNumber));

        public (long LvLowerBound, long CountedItems) GetFilterMapsLvProgress()
        {
            var raw = _rocks.Get(_controlColumnFamily, LvProgressKey);
            if (raw == null || raw.Length != 16) return (0L, 0L);
            var lowerBound = (long)RocksDbManager.Read64BE(raw.AsSpan(0, 8).ToArray());
            var counted = (long)RocksDbManager.Read64BE(raw.AsSpan(8, 8).ToArray());
            return (lowerBound, counted);
        }

        public void SetFilterMapsLvProgress(long lvLowerBound, long countedItems)
        {
            var buf = new byte[16];
            Array.Copy(RocksDbManager.Write64BE((ulong)lvLowerBound), 0, buf, 0, 8);
            Array.Copy(RocksDbManager.Write64BE((ulong)countedItems), 0, buf, 8, 8);
            _rocks.Put(_controlColumnFamily, LvProgressKey, buf);
        }
    }
}
