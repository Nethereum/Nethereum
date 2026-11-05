using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests.SecurityAudit71.S14
{
    public class ConsensusSszHostileOffsetS14Tests
    {
        private static readonly uint[] HostileWords =
            { 0u, 1u, 4u, 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFFu, 0xFFFFFFFCu, 0x00010000u };

        private static readonly ConsensusFork[] Forks =
            { ConsensusFork.Altair, ConsensusFork.Bellatrix, ConsensusFork.Capella, ConsensusFork.Deneb, ConsensusFork.Electra };

        private static readonly Dictionary<string, Func<byte[], ConsensusFork, object>> Decoders =
            new Dictionary<string, Func<byte[], ConsensusFork, object>>
            {
                ["LightClientUpdate"] = (b, f) => LightClientUpdate.Decode(b, f),
                ["LightClientFinalityUpdate"] = (b, f) => LightClientFinalityUpdate.Decode(b, f),
                ["LightClientOptimisticUpdate"] = (b, f) => LightClientOptimisticUpdate.Decode(b, f),
                ["LightClientBootstrap"] = (b, f) => LightClientBootstrap.Decode(b, f),
                ["LightClientHeader"] = (b, f) => LightClientHeader.Decode(b, f),
                ["ExecutionPayloadHeader"] = (b, f) => ExecutionPayloadHeader.Decode(b, f)
            };

        public static IEnumerable<object[]> DecoderNames() => Decoders.Keys.Select(k => new object[] { k });

        [Theory]
        [MemberData(nameof(DecoderNames))]
        [Trait("Rule", "S14-6")]
        public void Given_HostileOffsetWordsAtEveryFourByteSlot_When_Decoded_Then_OnlyTheStrictDecoderExceptionsAreRaisedAndAllocationIsBounded(string decoderName)
        {
            var decode = Decoders[decoderName];
            var random = new Random(14);
            var unexpected = new List<string>();
            long worstAllocation = 0;

            foreach (var fork in Forks)
            {
                foreach (var length in new[] { 0, 4, 100, 400, 1000, 25_000, 30_000 })
                {
                    for (var slot = 0; slot * 4 + 4 <= Math.Min(length, 1024); slot += length > 1000 ? 16 : 1)
                    {
                        foreach (var word in HostileWords)
                        {
                            var data = new byte[length];
                            random.NextBytes(data);
                            BitConverter.GetBytes(word).CopyTo(data, slot * 4);

                            var before = GC.GetAllocatedBytesForCurrentThread();
                            var thrown = Record.Exception(() => decode(data, fork));
                            worstAllocation = Math.Max(worstAllocation, GC.GetAllocatedBytesForCurrentThread() - before);

                            if (thrown != null && !(thrown is InvalidOperationException) && !(thrown is ArgumentException))
                                unexpected.Add($"{fork} len={length} slot={slot} word={word:X8} -> {thrown.GetType().Name}");
                        }
                    }
                }
            }

            Assert.True(worstAllocation < 8L * 1024 * 1024, $"worst allocation {worstAllocation}");
            Assert.True(unexpected.Count == 0, $"{unexpected.Count} unexpected exception types, first: {unexpected.FirstOrDefault()}");
        }
    }
}
