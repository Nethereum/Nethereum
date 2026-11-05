using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14.Round2
{
    [Trait("Category", "TimingSensitive")]
    public class AbiRound2S14Tests
    {
        private static byte[] Word(ulong value)
        {
            var word = new byte[32];
            for (var i = 0; i < 8; i++) word[31 - i] = (byte)(value >> (8 * i));
            return word;
        }

        private static byte[] Join(IEnumerable<byte[]> parts) => parts.SelectMany(p => p).ToArray();

        private static byte[] AliasedLevel(int count, byte[] child)
        {
            var parts = new List<byte[]> { Word((ulong)count) };
            for (var i = 0; i < count; i++) parts.Add(Word((ulong)(count * 32)));
            parts.Add(child);
            return Join(parts);
        }

        private static byte[] AliasedNesting(int depth, int count)
        {
            var block = Join(new[] { Word(1), Word(7) });
            for (var level = 1; level < depth; level++) block = AliasedLevel(count, block);
            return Join(new[] { Word(32), block });
        }

        private static long Allocated(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(action);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        [Theory]
        [InlineData(4, 20)]
        [InlineData(6, 8)]
        [Trait("Rule", "S14-R2-1")]
        public void Given_NestedArraysWhoseElementOffsetsAllAliasTheSameChild_When_Decoded_Then_WorkAndOutputStayLinearInTheInput(int depth, int count)
        {
            var payload = AliasedNesting(depth, count);
            var parameter = new Parameter("uint256" + Repeat("[]", depth), "a", 1);

            var allocated = Allocated(() => new ParameterDecoder().DecodeDefaultData(payload, parameter));

            Assert.True(allocated < 64L * payload.Length,
                $"{payload.Length} byte input at depth {depth} x {count} allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-R2-2")]
        public void Given_AWideSignatureOfSixteenThousandParameters_When_Deserialised_Then_ParsingStaysNearLinear()
        {
            var signature = "function f(" + Repeat("uint256,", 16_000) + "uint256)";
            var clock = Stopwatch.StartNew();

            Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(signature));

            Assert.True(clock.ElapsedMilliseconds < 20000, $"{signature.Length} char signature took {clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R2-3")]
        public void Given_SixtyFourBytesParametersWithAlternatingOverlappingOffsets_When_Decoded_Then_CopyingStaysNearTheInputSize()
        {
            const int fields = 64;
            const int span = 4096;
            var head = new List<byte[]>();
            for (var i = 0; i < fields; i++) head.Add(Word(i % 2 == 0 ? (ulong)(fields * 32) : (ulong)(fields * 32 + span)));
            var payload = Join(head.Concat(new[] { new byte[span * 2] }));
            var parameters = Enumerable.Range(0, fields).Select(i => new Parameter("bytes", "p" + i, i + 1)).ToArray();

            var allocated = Allocated(() => new ParameterDecoder().DecodeDefaultData(payload, parameters));

            Assert.True(allocated < 8L * payload.Length, $"{payload.Length} byte payload allocated {allocated} bytes");
        }

        [Theory]
        [InlineData(32)]
        [InlineData(33)]
        [Trait("Rule", "S14-R2-4")]
        public void Given_TheDepthLimitBoundary_When_SignaturesAreParsed_Then_ThirtyTwoPassesAndThirtyThreeIsRejected(int depth)
        {
            var signature = "function f(" + Repeat("(", depth) + "uint256" + Repeat(")", depth) + ")";

            var thrown = Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(signature));

            if (depth <= 32) Assert.Null(thrown); else Assert.NotNull(thrown);
        }

        [Fact]
        [Trait("Rule", "S14-R2-4")]
        public void Given_DeepParameterTextPassedStraightToTheSignatureEntryPoints_When_Extracted_Then_EveryEntryPointRejectsIt()
        {
            var deep = Repeat("(", 200) + "uint256" + Repeat(")", 200);
            var parser = new ABIStringSignatureDeserialiser();

            Assert.ThrowsAny<Exception>(() => parser.ExtractFunctionABI("function f()", "f", deep));
            Assert.ThrowsAny<Exception>(() => parser.ExtractEventABI("event e()", "e", deep));
            Assert.ThrowsAny<Exception>(() => parser.ExtractErrorABI("error r()", "r", deep));
            Assert.ThrowsAny<Exception>(() => parser.ExtractConstructorABI("constructor()", "", deep));
            Assert.ThrowsAny<Exception>(() => parser.ExtractParameters(deep));
        }

        [Fact]
        [Trait("Rule", "S14-R2-4")]
        public void Given_ReturnsClauseAndUnbalancedClosersAndInnerArrayBrackets_When_Parsed_Then_TheLimitsStillHold()
        {
            var deepReturns = "function f() returns (" + Repeat("(", 200) + "uint256" + Repeat(")", 200) + ")";
            var closersFirst = "function f(" + Repeat(")", 50_000) + Repeat("(", 100) + ")";
            var manyBrackets = "function f(uint256" + Repeat("[]", 200) + ")";
            var innerBrackets = "function f(" + Repeat("(", 30) + "uint256" + Repeat("[]", 32) + Repeat(")", 30) + Repeat("[]", 32) + ")";

            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(deepReturns));
            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(closersFirst));
            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(manyBrackets));
            var clock = Stopwatch.StartNew();
            Assert.Null(Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(innerBrackets)));
            Assert.True(clock.ElapsedMilliseconds < 20000);
        }

        private static string JsonWithComponents(string type, int depth)
        {
            var json = new StringBuilder("[{\"type\":\"function\",\"name\":\"f\",\"inputs\":[");
            for (var i = 0; i < depth; i++) json.Append("{\"name\":\"a\",\"type\":\"" + type + "\",\"components\":[");
            json.Append("{\"name\":\"x\",\"type\":\"uint256\"}");
            for (var i = 0; i < depth; i++) json.Append("]}");
            json.Append("],\"outputs\":[]}]");
            return json.ToString();
        }

        [Theory]
        [InlineData("uint256")]
        [InlineData("tuple")]
        [InlineData("tuple[]")]
        [Trait("Rule", "S14-R2-5")]
        public void Given_JsonComponentsNested200DeepOnAnyParameterType_When_Deserialised_Then_ItIsRejected(string type)
        {
            Assert.ThrowsAny<Exception>(() => ABIDeserialiserFactory.DeserialiseContractABI(JsonWithComponents(type, 200)));
        }

        [Fact]
        [Trait("Rule", "S14-R2-5")]
        public void Given_ADeepJArrayHandedToTheJArrayOverload_When_Deserialised_Then_ItIsRejected()
        {
            JArray array;
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(JsonWithComponents("tuple", 60))) { MaxDepth = null })
            {
                array = JArray.Load(reader);
            }

            Assert.ThrowsAny<Exception>(() => new ABIJsonDeserialiser().DeserialiseContract(array));
        }

        [Fact]
        [Trait("Rule", "S14-R2-5")]
        public void Given_DeepComponentsThroughTheSystemTextJsonDeserialiser_When_Deserialised_Then_ItIsRejected()
        {
            Assert.ThrowsAny<Exception>(() => new ABIJsonDeserialiserSTJ().DeserialiseContract(JsonWithComponents("tuple", 5000)));
        }
    }
}
