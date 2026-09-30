using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Storage.History;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage.History
{
    public class HistoryKeysTests
    {
        [Fact]
        public void BlockKey_IsEightBytes_And_RoundTrips()
        {
            foreach (var n in new ulong[] { 0, 1, 255, 256, 1_000_000, uint.MaxValue, ulong.MaxValue })
            {
                var k = HistoryKeys.BlockKey(n);
                Assert.Equal(8, k.Length);
                Assert.Equal(n, HistoryKeys.ReadBlockNumber(k));
            }
        }

        [Fact]
        public void TxKey_IsTwelveBytes_And_RoundTrips()
        {
            foreach (var (n, i) in new (ulong, uint)[] { (0, 0), (0, 1), (1, 0), (1_000_000, 250), (ulong.MaxValue, uint.MaxValue) })
            {
                var k = HistoryKeys.TxKey(n, i);
                Assert.Equal(12, k.Length);
                Assert.Equal(n, HistoryKeys.ReadBlockNumber(k));
                Assert.Equal(i, HistoryKeys.ReadTxIndex(k));
            }
        }

        [Fact]
        public void TxKey_OrdersWithinBlock_ThenAcrossBlocks()
        {
            Assert.True(Cmp(HistoryKeys.TxKey(5, 0), HistoryKeys.TxKey(5, 1)) < 0);
            Assert.True(Cmp(HistoryKeys.TxKey(5, 1), HistoryKeys.TxKey(5, 2)) < 0);
            Assert.True(Cmp(HistoryKeys.TxKey(5, uint.MaxValue), HistoryKeys.TxKey(6, 0)) < 0);
            Assert.True(Cmp(HistoryKeys.TxKey(5, 999_999), HistoryKeys.TxKey(6, 0)) < 0);
        }

        [Fact]
        public void BlockKey_Orders_ByNumber()
        {
            Assert.True(Cmp(HistoryKeys.BlockKey(0), HistoryKeys.BlockKey(1)) < 0);
            Assert.True(Cmp(HistoryKeys.BlockKey(255), HistoryKeys.BlockKey(256)) < 0);
            Assert.True(Cmp(HistoryKeys.BlockKey(uint.MaxValue), HistoryKeys.BlockKey((ulong)uint.MaxValue + 1)) < 0);
        }

        [Fact]
        public void ByteOrder_EqualsNumericOrder_OverShuffledEdgeCases()
        {
            var pairs = new List<(ulong n, uint i)>();
            foreach (var n in new ulong[] { 0, 1, 255, 256, 65_535, 65_536, 16_777_215, 16_777_216, 1_000_000, uint.MaxValue, (ulong)uint.MaxValue + 1, ulong.MaxValue })
                foreach (var i in new uint[] { 0, 1, 255, 256, 65_535, 65_536, uint.MaxValue })
                    pairs.Add((n, i));

            var shuffled = pairs.AsEnumerable().Reverse().ToList();

            var byBytes = shuffled.OrderBy(p => HistoryKeys.TxKey(p.n, p.i), ByteArrayComparer.Instance)
                                  .ToList();
            var byNumber = shuffled.OrderBy(p => p.n).ThenBy(p => p.i).ToList();

            Assert.Equal(byNumber, byBytes);
        }

        private static int Cmp(byte[] a, byte[] b) => ByteArrayComparer.Instance.Compare(a, b);

        private sealed class ByteArrayComparer : IComparer<byte[]>
        {
            public static readonly ByteArrayComparer Instance = new ByteArrayComparer();
            public int Compare(byte[] a, byte[] b)
            {
                int len = Math.Min(a.Length, b.Length);
                for (int k = 0; k < len; k++)
                {
                    int d = a[k].CompareTo(b[k]);
                    if (d != 0) return d;
                }
                return a.Length.CompareTo(b.Length);
            }
        }
    }
}
