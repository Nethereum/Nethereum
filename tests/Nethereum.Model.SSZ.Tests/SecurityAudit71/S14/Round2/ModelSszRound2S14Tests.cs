using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests.SecurityAudit71.S14.Round2
{
    [Trait("Category", "TimingSensitive")]
    public class ModelSszRound2S14Tests
    {
        private static readonly uint[] HostileWords =
            { 0u, 1u, 4u, 8u, 24u, 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFFu, 0xFFFFFFFCu, 0x01000000u };

        private static readonly SszBlockEncodingProvider Provider = new SszBlockEncodingProvider();

        private static IEnumerable<(string name, Action<byte[]> decode)> Decoders()
        {
            yield return ("DecodeTransaction", b => Provider.DecodeTransaction(b));
            yield return ("DecodeReceipt", b => Provider.DecodeReceipt(b));
            yield return ("DecodeBlockHeader", b => Provider.DecodeBlockHeader(b));
            yield return ("DecodeLog", b => Provider.DecodeLog(b));
            yield return ("DecodeAccessList", b => SszAccessListEncoder.Current.DecodeAccessList(b));
            yield return ("DecodeAccessTuple", b => SszAccessListEncoder.Current.DecodeAccessTuple(b));
            yield return ("DecodeAuthorisationList", b => SszTransactionEncoder.Current.DecodeAuthorisationList(b));
            yield return ("Decode1559", b => SszTransactionEncoder.Current.DecodeTransaction1559Payload(b, false, null));
            yield return ("Decode4844", b => SszTransactionEncoder.Current.DecodeTransaction4844Payload(b));
            yield return ("Decode7702", b => SszTransactionEncoder.Current.DecodeTransaction7702Payload(b));
        }

        public static IEnumerable<object[]> DecoderNames() => Decoders().Select(d => new object[] { d.name });

        [Theory]
        [MemberData(nameof(DecoderNames))]
        [Trait("Rule", "S14-R2-6")]
        public void Given_HostileOffsetWordsAtEveryFourByteSlot_When_Decoded_Then_AllocationAndTimeStayLinearInTheInput(string name)
        {
            var decode = Decoders().First(d => d.name == name).decode;
            var random = new Random(61);
            var clock = Stopwatch.StartNew();
            var worstRatio = 0.0;
            string worst = null;

            foreach (var length in new[] { 0, 8, 64, 200, 500, 1200, 4000 })
            {
                for (var slot = 0; slot * 4 + 4 <= Math.Min(length, 600); slot++)
                {
                    foreach (var word in HostileWords)
                    {
                        var data = new byte[length];
                        random.NextBytes(data);
                        if (length > 0 && random.Next(3) == 0) data[0] = (byte)random.Next(0, 4);
                        BitConverter.GetBytes(word).CopyTo(data, slot * 4);

                        var before = GC.GetAllocatedBytesForCurrentThread();
                        Record.Exception(() => decode(data));
                        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        var ratio = (allocated - 4096.0) / Math.Max(length, 1);
                        if (ratio > worstRatio)
                        {
                            worstRatio = ratio;
                            worst = $"len={length} slot={slot} word={word:X8} allocated={allocated}";
                        }
                    }
                }
            }

            Assert.True(worstRatio < 64, $"{name}: worst allocation ratio {worstRatio:F1}x at {worst}");
            Assert.True(clock.ElapsedMilliseconds < 200000, $"{name} took {clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R2-6")]
        public void Given_AnAccessListWithAscendingEqualOffsetsAndManyItems_When_Decoded_Then_WorkStaysLinear()
        {
            const int items = 20_000;
            var data = new byte[items * 4 + 20 + 4];
            for (var i = 0; i < items; i++) BitConverter.GetBytes((uint)(items * 4)).CopyTo(data, i * 4);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            Record.Exception(() => SszAccessListEncoder.Current.DecodeAccessList(data));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < 64L * data.Length, $"{data.Length} bytes allocated {allocated}");
            Assert.True(clock.ElapsedMilliseconds < 20000);
        }
    }
}
