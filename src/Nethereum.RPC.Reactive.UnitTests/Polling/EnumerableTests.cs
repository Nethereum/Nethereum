using System;
using System.Linq;
using System.Numerics;
using Microsoft.Reactive.Testing;
using Nethereum.RPC.Reactive.Polling;
using Xunit;

namespace Nethereum.RPC.Reactive.UnitTests.Polling
{
    public class EnumerableTests
    {
        [Fact]
        public void Range()
        {
            var start = new BigInteger(5);
            var count = new BigInteger(10);

            var xs = EnumerableExtensions.Range(start, count).ToList();
            var ys = new[]
            {
                new BigInteger(5), new BigInteger(6), new BigInteger(7), new BigInteger(8), new BigInteger(9), new BigInteger(10), new BigInteger(11), new BigInteger(12), new BigInteger(13), new BigInteger(14)
            };

            xs.AssertEqual(ys);
        }

        [Fact]
        public void Range_CountOutOfRange()
        {
            var start = new BigInteger(5);
            var countValid = new BigInteger(0);
            var countInvalid = new BigInteger(-2);

            Assert.False(EnumerableExtensions.Range(start, countValid).Any());
            Assert.Throws<ArgumentOutOfRangeException>(() => EnumerableExtensions.Range(start, countInvalid).ToList());
        }
    }
}