using System;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.History
{
    internal static class HistoryRows
    {
        public static ulong? Tip(Iterator it)
        {
            it.SeekToLast();
            return it.Valid() ? HistoryKeys.TryReadBlockNumber(it.Key()) : null;
        }

        public static void ScanBlock(Iterator it, ulong blockNumber, Action<byte[]> onRow)
        {
            for (it.Seek(HistoryKeys.TxKey(blockNumber, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != blockNumber) break;
                onRow(it.Value());
            }
        }
    }
}
