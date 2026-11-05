using System;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Ssz.Tests.SecurityAudit71.S14
{
    public class SszReaderDeclaredCountS14Tests
    {
        private const int DeclaredCount = 1 << 24;
        private const long Budget = 1L << 20;

        private static long Allocated(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(action);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        [Trait("Rule", "S14-5")]
        public void Given_AListCountOfSixteenMillionOverEightBytes_When_ReadList_Then_AllocationIsBoundedByTheInput()
        {
            var data = new byte[8];

            var allocated = Allocated(() =>
            {
                var reader = new SszReader(data);
                SszReader.ReadList<ulong>(ref reader, DeclaredCount);
            });

            Assert.True(allocated < Budget, $"8 byte input allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-5")]
        public void Given_AVectorCountOfSixteenMillionOverThirtyTwoBytes_When_ReadVector_Then_AllocationIsBoundedByTheInput()
        {
            var data = new byte[32];

            var allocated = Allocated(() =>
            {
                var reader = new SszReader(data);
                reader.ReadVector(DeclaredCount, 32);
            });

            Assert.True(allocated < Budget, $"32 byte input allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-5")]
        public void Given_AVariableBytesLengthOfFourBillion_When_Read_Then_ItThrowsWithoutAllocating()
        {
            var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3, 4 };

            var allocated = Allocated(() =>
            {
                var reader = new SszReader(data);
                reader.ReadVariableBytes();
            });

            Assert.True(allocated < Budget);
        }

        [Fact]
        [Trait("Rule", "S14-5")]
        public void Given_AFixedReadOfIntMaxAfterAnOffset_When_Read_Then_ItThrowsTheSszBufferException()
        {
            var data = new byte[16];
            var thrown = Record.Exception(() =>
            {
                var reader = new SszReader(data);
                reader.ReadUInt32();
                reader.ReadFixedBytes(int.MaxValue);
            });

            Assert.IsType<InvalidOperationException>(thrown);
        }
    }
}
