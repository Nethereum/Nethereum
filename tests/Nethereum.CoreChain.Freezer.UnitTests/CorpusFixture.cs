using System;
using System.IO;
using Nethereum.Freezer;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    internal static class CorpusFixture
    {
        public static string TxsSliceDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient-txs");
        public static string GenesisSliceDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        public static byte[] ReadRawItem(string fixtureDir, string tableName, bool compressed, long itemNumber)
        {
            using var table = OpenRawTable(fixtureDir, tableName, compressed);
            return table.Read(itemNumber);
        }

        public static long ItemCount(string fixtureDir, string tableName, bool compressed)
        {
            using var table = OpenRawTable(fixtureDir, tableName, compressed);
            return table.Count;
        }

        private static FreezerTable<byte[]> OpenRawTable(string fixtureDir, string tableName, bool compressed)
        {
            var paths = new FreezerTablePaths(fixtureDir, tableName, compressed);
            IItemCodec<byte[]> codec = compressed
                ? new SnappyItemCodec<byte[]>(new IdentityByteCodec())
                : new IdentityByteCodec();
            return FreezerTable<byte[]>.OpenReadOnly(paths, codec);
        }
    }
}
