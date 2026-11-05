using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Model.UnitTests.SecurityAudit71.S14
{
    [Trait("Category", "TimingSensitive")]
    public class ModelRlpDecodersHostileShapeS14Tests
    {
        private static byte[] Header(int prefixBase, int length)
        {
            if (length < 56) return new[] { (byte)(prefixBase + length) };
            var lengthBytes = BitConverter.GetBytes(length).Reverse().SkipWhile(b => b == 0).ToArray();
            return new[] { (byte)(prefixBase + 55 + lengthBytes.Length) }.Concat(lengthBytes).ToArray();
        }

        private static byte[] Bytes(int size)
        {
            var payload = Enumerable.Repeat((byte)0x7F, size).ToArray();
            if (size == 1) return new byte[] { 0x81, 0x7F };
            return Header(0x80, size).Concat(payload).ToArray();
        }

        private static byte[] ListOf(int itemCount, int itemSize)
        {
            var item = Bytes(itemSize);
            var body = Enumerable.Range(0, itemCount).SelectMany(_ => item).ToArray();
            return Header(0xC0, body.Length).Concat(body).ToArray();
        }

        public static IEnumerable<object[]> Decoders()
        {
            yield return new object[] { "legacy-or-typed tx", (Action<byte[]>)(b => TransactionFactory.CreateTransaction(b)) };
            yield return new object[] { "typed 0x01", (Action<byte[]>)(b => TransactionFactory.CreateTransaction(new byte[] { 1 }.Concat(b).ToArray())) };
            yield return new object[] { "typed 0x02", (Action<byte[]>)(b => TransactionFactory.CreateTransaction(new byte[] { 2 }.Concat(b).ToArray())) };
            yield return new object[] { "typed 0x03", (Action<byte[]>)(b => TransactionFactory.CreateTransaction(new byte[] { 3 }.Concat(b).ToArray())) };
            yield return new object[] { "typed 0x04", (Action<byte[]>)(b => TransactionFactory.CreateTransaction(new byte[] { 4 }.Concat(b).ToArray())) };
            yield return new object[] { "header", (Action<byte[]>)(b => BlockHeaderEncoder.Current.Decode(b)) };
            yield return new object[] { "receipt", (Action<byte[]>)(b => ReceiptEncoder.Current.Decode(b)) };
            yield return new object[] { "block accessList", (Action<byte[]>)(b => new BlockAccessListRLPEncoder().Decode(b)) };
        }

        [Theory]
        [MemberData(nameof(Decoders))]
        [Trait("Rule", "S14-8")]
        public void Given_HostileListShapes_When_Decoded_Then_WorkAndAllocationStayLinearInTheInput(string name, Action<byte[]> decode)
        {
            foreach (var (count, size) in new[] { (0, 0), (1, 100_000), (20, 1), (20, 5_000), (50_000, 1), (3_000, 0), (200, 1_000) })
            {
                var payload = ListOf(count, size);
                var before = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                Record.Exception(() => decode(payload));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.True(allocated < 256L * payload.Length + (1L << 20),
                    $"{name} ({count}x{size}, {payload.Length} bytes) allocated {allocated}");
                Assert.True(clock.ElapsedMilliseconds < 20000, $"{name} ({count}x{size}) took {clock.ElapsedMilliseconds} ms");
            }
        }
    }
}
