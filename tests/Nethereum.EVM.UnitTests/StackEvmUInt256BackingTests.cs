using Nethereum.EVM;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class StackEvmUInt256BackingTests
    {
        private static Program NewProgram() => new Program(new byte[] { 0x00 });

        public static System.Collections.Generic.IEnumerable<object[]> Words()
        {
            yield return new object[] { new byte[0] };
            yield return new object[] { new byte[] { 0x05 } };
            yield return new object[] { new byte[] { 0x01, 0x00 } };
            yield return new object[] { new byte[20] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20 } };
            var full = new byte[32]; for (int i = 0; i < 32; i++) full[i] = (byte)(i + 1);
            yield return new object[] { full };
        }

        [Theory]
        [MemberData(nameof(Words))]
        public void PushBytes_Pop_ByteIdenticalTo_PadTo32Bytes(byte[] word)
        {
            var p = NewProgram();
            p.StackPush(word);
            Assert.Equal(word.PadTo32Bytes(), p.StackPop());
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(255UL)]
        [InlineData(ulong.MaxValue)]
        public void PushU256_PopU256_LosslessRoundTrip(ulong value)
        {
            var p = NewProgram();
            var v = new EvmUInt256(value);
            p.StackPush(v);
            Assert.Equal(v, p.StackPopU256());
        }

        [Fact]
        public void PushU256_MaxValue_RoundTrips()
        {
            var p = NewProgram();
            p.StackPush(EvmUInt256.MaxValue);
            Assert.Equal(EvmUInt256.MaxValue, p.StackPopU256());
        }

        [Fact]
        public void Dup_ProducesIndependentSlot()
        {
            var p = NewProgram();
            var a = new EvmUInt256(0xABCDEF);
            p.StackPush(a);
            p.StackDup(1);
            Assert.Equal(a, p.StackPopU256());
            Assert.Equal(a, p.StackPopU256());
        }

        [Fact]
        public void Swap_ExchangesTopWithIndexedSlot()
        {
            var p = NewProgram();
            var bottom = new EvmUInt256(11);
            var top = new EvmUInt256(22);
            p.StackPush(bottom);
            p.StackPush(top);
            p.StackSwap(1);
            Assert.Equal(bottom, p.StackPopU256());
            Assert.Equal(top, p.StackPopU256());
        }

        [Fact]
        public void HotPath_PushDupPop_DoesNotHeapAllocate()
        {
            var p = NewProgram();
            var v = new EvmUInt256(0x123456789A);
            for (int i = 0; i < 2000; i++) { p.StackPush(v); p.StackDup(1); p.StackPopU256(); p.StackPopU256(); }
            const int N = 200_000;
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < N; i++) { p.StackPush(v); p.StackDup(1); p.StackPopU256(); p.StackPopU256(); }
            var bytesPerIter = (double)(System.GC.GetAllocatedBytesForCurrentThread() - before) / N;
            Assert.True(bytesPerIter < 8.0, $"expected ~0 B/iter (push+dup+2pop), got {bytesPerIter:F1} B/iter");
        }
    }
}
