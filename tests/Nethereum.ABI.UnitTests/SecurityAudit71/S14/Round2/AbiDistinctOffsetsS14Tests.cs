using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14.Round2
{
    public class AbiDistinctOffsetsS14Tests
    {
        private const int MaxDepth = 32;

        private static byte[] Word(ulong value)
        {
            var word = new byte[32];
            for (var i = 0; i < 8; i++) word[31 - i] = (byte)(value >> (8 * i));
            return word;
        }

        private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private static byte[] NestedArrays(int depth)
        {
            var block = Join(Word(1), Word(7));
            for (var level = 1; level < depth; level++)
                block = Join(Word(2), Word(96), Word(64), Word(0), block);
            return Join(Word(32), block);
        }

        private static byte[] NestedTuples(int depth)
        {
            var block = Join(Word(32), Word(0));
            for (var level = 1; level <= depth; level++)
                block = Join(Word(96), Word(64), Word(0), block);
            return Join(Word(32), block);
        }

        private static Parameter TupleParameter(int depth)
        {
            var parameter = new Parameter("tuple", "t", 1);
            Fill((TupleType)parameter.ABIType, depth);
            return parameter;
        }

        private static void Fill(TupleType tuple, int depth)
        {
            if (depth == 0)
            {
                tuple.SetComponents(new[] { new Parameter("bytes", "a", 1) });
                return;
            }

            var child = new Parameter("tuple", "child", 1);
            Fill((TupleType)child.ABIType, depth - 1);
            tuple.SetComponents(new[] { child, new Parameter("bytes", "pad", 2) });
        }

        private static long Allocated(Action action)
        {
            action();
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        [Trait("Rule", "S14-R2-1")]
        public void Given_ArraysNestedAtTheDepthLimitWithDistinctNonAscendingOffsets_When_Decoded_Then_TheyDecodeAndAllocationStaysWithinAFixedMultipleOfTheInput()
        {
            var payload = NestedArrays(MaxDepth);
            var parameter = new Parameter("uint256" + string.Concat(Enumerable.Repeat("[]", MaxDepth)), "a", 1);

            var allocated = Allocated(() => new ParameterDecoder().DecodeDefaultData(payload, parameter));

            Assert.True(allocated < 64L * payload.Length, $"{payload.Length} byte payload allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-R2-1")]
        public void Given_TuplesNestedAtTheDepthLimitWithDistinctNonAscendingOffsets_When_Decoded_Then_TheyDecodeAndAllocationStaysWithinAFixedMultipleOfTheInput()
        {
            var payload = NestedTuples(MaxDepth);
            var parameter = TupleParameter(MaxDepth);

            var allocated = Allocated(() => new ParameterDecoder().DecodeDefaultData(payload, parameter));

            Assert.True(allocated < 64L * payload.Length, $"{payload.Length} byte payload allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-R2-1")]
        public void Given_TwoTupleFieldsPointingAtTheSameChild_When_Decoded_Then_ItIsRejected()
        {
            var child = Join(Word(32), Word(0));
            var tuple = Join(Word(64), Word(64), child);
            var payload = Join(Word(32), tuple);
            var parameter = new Parameter("tuple", "t", 1);
            ((TupleType)parameter.ABIType).SetComponents(new[] { new Parameter("bytes", "a", 1), new Parameter("bytes", "b", 2) });

            Assert.ThrowsAny<Exception>(() => new ParameterDecoder().DecodeDefaultData(payload, parameter));
        }

        [Fact]
        [Trait("Rule", "S14-R2-1")]
        public void Given_TwoTopLevelParametersPointingAtTheSameData_When_Decoded_Then_ItIsRejected()
        {
            var payload = Join(Word(64), Word(64), Word(0));
            var parameters = new[] { new Parameter("bytes", "a", 1), new Parameter("bytes", "b", 2) };

            Assert.ThrowsAny<Exception>(() => new ParameterDecoder().DecodeDefaultData(payload, parameters));
        }

        [Fact]
        [Trait("Rule", "S14-R2-1")]
        public void Given_DistinctNonAscendingOffsets_When_Decoded_Then_EveryValueIsRecovered()
        {
            var swapped = Join(Word(32), Word(2), Word(128), Word(64), Word(1), Word(9), Word(2), Word(1), Word(2));
            var parameter = new Parameter("uint256[][]", "a", 1);

            var decoded = new ParameterDecoder().DecodeDefaultData(swapped, parameter);

            var outer = (List<List<System.Numerics.BigInteger>>)decoded[0].Result;
            Assert.Equal(2, outer.Count);
            Assert.Equal(new System.Numerics.BigInteger(9), outer[1][0]);
            Assert.Equal(2, outer[0].Count);
        }
    }
}
