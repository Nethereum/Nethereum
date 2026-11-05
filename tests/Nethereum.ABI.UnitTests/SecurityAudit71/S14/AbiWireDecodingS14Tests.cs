using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14
{
    public class AbiWireDecodingS14Tests
    {
        private const int HostileDepth = 256;

        private sealed class TripwireException : Exception
        {
        }

        private sealed class CountingDecoder : TypeDecoder
        {
            public int Calls;
            public int TripAfter;

            public override bool IsSupportedType(Type type) => true;

            public override object Decode(byte[] encoded, Type type)
            {
                if (++Calls > TripAfter) throw new TripwireException();
                return new object();
            }

            public override object DecodePacked(byte[] encoded, Type type) => Decode(encoded, type);

            public override Type GetDefaultDecodingType() => typeof(object);
        }

        private sealed class CountingWordType : ABIType
        {
            public CountingWordType(CountingDecoder decoder) : base("counting")
            {
                Decoder = decoder;
            }
        }

        private static byte[] Word(ulong value)
        {
            var word = new byte[32];
            for (var i = 0; i < 8; i++) word[31 - i] = (byte)(value >> (8 * i));
            return word;
        }

        private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        [Theory]
        [InlineData(1UL << 26)]
        [InlineData(1UL << 27)]
        [Trait("Rule", "S14-1")]
        public void Given_ADynamicArrayDeclaringSizeWhoseTimes32WrapsInt32_When_Decoded_Then_ItIsRejectedBeforeAnyElementIsDecoded(ulong declaredSize)
        {
            var counting = new CountingDecoder { TripAfter = 10_000 };
            var decoder = new DynamicArrayTypeDecoder(new CountingWordType(counting));
            var payload = Concat(Word(declaredSize), new byte[64]);

            var thrown = Record.Exception(() => decoder.Decode(payload, typeof(List<object>)));

            Assert.NotNull(thrown);
            Assert.IsNotType<TripwireException>(thrown);
            Assert.True(counting.Calls <= 2, $"{counting.Calls} element decodes ran for a 96 byte payload declaring {declaredSize} elements");
        }

        [Fact]
        [Trait("Rule", "S14-1")]
        public void Given_AStaticArrayTypeWhoseSizeTimes32WrapsInt32_When_Decoded_Then_ItIsRejectedBeforeAnyElementIsDecoded()
        {
            var counting = new CountingDecoder { TripAfter = 10_000 };
            var decoder = new ArrayTypeDecoder(new CountingWordType(counting), 1 << 26);

            var thrown = Record.Exception(() => decoder.Decode(new byte[64], typeof(List<object>)));

            Assert.NotNull(thrown);
            Assert.IsNotType<TripwireException>(thrown);
            Assert.True(counting.Calls <= 2, $"{counting.Calls} element decodes ran for a 64 byte payload");
        }

        [Fact]
        [Trait("Rule", "S14-2")]
        public void Given_AStringArrayWithOverlappingOffsets_When_Decoded_Then_CopyingStaysLinearInThePayload()
        {
            const int count = 2000;
            var head = new List<byte[]>();
            var tailStart = count * 32;
            var span = count * 32;
            for (var i = 0; i < count; i++)
                head.Add(Word(i % 2 == 0 ? 0UL : (ulong)(tailStart + 32 + span)));
            var array = Concat(new[] { Word(count) }.Concat(head).Concat(new[] { new byte[span + 64] }).ToArray());
            var payload = Concat(Word(32), array);
            var parameter = new Parameter("string[]", "a", 1);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(() => new ParameterDecoder().DecodeDefaultData(payload, parameter));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < 64L * payload.Length,
                $"decoding {payload.Length} bytes allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-3")]
        public void Given_AJsonAbiWithTupleComponentsNested256Deep_When_Deserialised_Then_ItIsRejectedByADepthBound()
        {
            var json = new StringBuilder("[{\"type\":\"function\",\"name\":\"f\",\"inputs\":[");
            for (var i = 0; i < HostileDepth; i++)
                json.Append("{\"name\":\"a\",\"type\":\"tuple\",\"components\":[");
            json.Append("{\"name\":\"x\",\"type\":\"uint256\"}");
            for (var i = 0; i < HostileDepth; i++) json.Append("]}");
            json.Append("],\"outputs\":[]}]");

            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(json.ToString()));
        }

        [Fact]
        [Trait("Rule", "S14-3")]
        public void Given_AStringSignatureWithTuplesNested256Deep_When_Deserialised_Then_ItIsRejectedByADepthBound()
        {
            var signature = "function f(" + new string('(', HostileDepth) + "uint256" + new string(')', HostileDepth) + ")";

            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(signature));
        }

        [Fact]
        [Trait("Rule", "S14-3")]
        public void Given_ATypeNameWithArrayDimensionsNested256Deep_When_Created_Then_ItIsRejectedByADepthBound()
        {
            var typeName = "uint256" + string.Concat(Enumerable.Repeat("[]", HostileDepth));

            Assert.ThrowsAny<Exception>(() => ABIType.CreateABIType(typeName));
        }
    }
}
