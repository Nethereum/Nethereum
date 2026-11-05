using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14.Round3
{
    [Trait("Category", "TimingSensitive")]
    public class AbiRound3S14Tests
    {
        private const long ConstantOverhead = 16 * 1024;

        private static byte[] Word(ulong value)
        {
            var word = new byte[32];
            for (var i = 0; i < 8; i++) word[31 - i] = (byte)(value >> (8 * i));
            return word;
        }

        private static byte[] Join(IEnumerable<byte[]> parts) => parts.SelectMany(p => p).ToArray();

        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        private static long Allocated(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(action);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static void AssertBounded(byte[] payload, Action decode, string what)
        {
            var clock = Stopwatch.StartNew();
            var allocated = Allocated(decode);
            Assert.True(allocated < 64L * payload.Length + ConstantOverhead,
                $"{what}: {payload.Length} byte payload allocated {allocated} bytes");
            Assert.True(clock.ElapsedMilliseconds < 30000, $"{what}: took {clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R3-1")]
        public void Given_BytesArrayElementsWhoseOffsetsDifferByOneByte_When_Decoded_Then_AllocationStaysLinear()
        {
            const int count = 1500;
            var parts = new List<byte[]> { Word(32), Word(count) };
            for (var i = 0; i < count; i++) parts.Add(Word((ulong)(count * 32 + i)));
            parts.Add(new byte[count + 64]);
            var payload = Join(parts);

            AssertBounded(payload, () => new ParameterDecoder().DecodeDefaultData(payload, new Parameter("bytes[]", "a", 1)), "1-byte-apart offsets");
        }

        [Fact]
        [Trait("Rule", "S14-R3-1")]
        public void Given_ElementOffsetsThatPointInsideTheArraysOwnHeadRegion_When_Decoded_Then_AllocationStaysLinear()
        {
            const int count = 1200;
            var parts = new List<byte[]> { Word(32), Word(count) };
            for (var i = 0; i < count; i++) parts.Add(Word((ulong)((count - i) * 32 - 7 + (i % 5))));
            parts.Add(new byte[2048]);
            var payload = Join(parts);

            AssertBounded(payload, () => new ParameterDecoder().DecodeDefaultData(payload, new Parameter("uint256[][]", "a", 1)), "offsets inside head");
        }

        [Fact]
        [Trait("Rule", "S14-R3-1")]
        public void Given_AMixedTupleWhoseDynamicOffsetsPointAtItsOwnStaticFields_When_Decoded_Then_AllocationStaysLinear()
        {
            var parameters = new[]
            {
                new Parameter("uint256", "x", 1),
                new Parameter("bytes", "y", 2),
                new Parameter("uint256[]", "z", 3),
                new Parameter("string", "w", 4)
            };
            var payload = Join(new[] { Word(5), Word(0), Word(32), Word(1), new byte[4096] });

            AssertBounded(payload, () => new ParameterDecoder().DecodeDefaultData(payload, parameters), "static/dynamic overlap");
        }

        [Fact]
        [Trait("Rule", "S14-R3-1")]
        public void Given_ASelfReferencingChild_When_DecodedAtDepth32_Then_AllocationStaysUnderTheSixtyFourFoldBound()
        {
            var depth = 32;
            var block = Join(new[] { Word(1), Word(7) });
            for (var level = 1; level < depth; level++)
                block = Join(new[] { Word(1), Word(32), block });
            var payload = Join(new[] { Word(32), block });
            var parameter = new Parameter("uint256" + Repeat("[]", depth), "a", 1);

            AssertBounded(payload, () => new ParameterDecoder().DecodeDefaultData(payload, parameter), "depth 32 chain");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [Trait("Rule", "S14-R3-1")]
        public void Given_RandomStructuredHeadsAtRandomDepthsUpTo32_When_Decoded_Then_AllocationStaysUnderTheSixtyFourFoldBound(int seed)
        {
            var random = new Random(seed);
            var leaves = new[] { "uint256", "bytes", "string" };
            for (var trial = 0; trial < 1500; trial++)
            {
                var depth = random.Next(2, 33);
                var type = leaves[random.Next(3)] + Repeat("[]", depth);
                var length = random.Next(1024, 20000) / 32 * 32;
                var mode = random.Next(5);
                var data = new byte[length];
                for (var i = 0; i + 32 <= length; i += 32)
                {
                    ulong value;
                    switch (mode)
                    {
                        case 0: value = (ulong)random.Next(0, 6); break;
                        case 1: value = (ulong)(random.Next(0, length / 32) * 32); break;
                        case 2: value = (ulong)random.Next(0, length); break;
                        case 3: value = (i / 32) % 2 == 0 ? (ulong)random.Next(1, 5) : (ulong)(i + 32 * random.Next(1, 4) + random.Next(0, 2)); break;
                        default: value = (ulong)((i / 32) * 32 + 32 + random.Next(0, 3) * 32 + random.Next(0, 2)); break;
                    }
                    Word(value).CopyTo(data, i);
                }
                var payload = Join(new[] { Word(32), data });
                var parameter = new Parameter(type, "a", 1);

                AssertBounded(payload, () => new ParameterDecoder().DecodeDefaultData(payload, parameter), $"seed {seed} trial {trial} {type} mode {mode}");
            }
        }

        [Fact]
        [Trait("Rule", "S14-R3-2")]
        public void Given_TheSolidityDocumentationEncodingOfFUint32ArrayBytes10Bytes_When_Decoded_Then_ValuesMatch()
        {
            var ten = new byte[32];
            Encoding.ASCII.GetBytes("1234567890").CopyTo(ten, 0);
            var hello = new byte[32];
            Encoding.ASCII.GetBytes("Hello, world!").CopyTo(hello, 0);
            var payload = Join(new[] { Word(0x123), Word(0x80), ten, Word(0xe0), Word(2), Word(0x456), Word(0x789), Word(13), hello });
            var parameters = new[]
            {
                new Parameter("uint256", "a", 1), new Parameter("uint32[]", "b", 2),
                new Parameter("bytes10", "c", 3), new Parameter("bytes", "d", 4)
            };

            var result = new ParameterDecoder().DecodeDefaultData(payload, parameters);

            Assert.Equal(new BigInteger(0x123), result[0].Result);
            Assert.Equal(new BigInteger[] { 0x456, 0x789 }, ((IEnumerable)result[1].Result).Cast<object>().Select(o => (BigInteger)o).ToArray());
            Assert.Equal(Encoding.ASCII.GetBytes("1234567890"), (byte[])result[2].Result);
            Assert.Equal(Encoding.ASCII.GetBytes("Hello, world!"), (byte[])result[3].Result);
        }

        [Fact]
        [Trait("Rule", "S14-R3-2")]
        public void Given_TheSolidityDocumentationEncodingOfGNestedUint256ArraysAndStringArray_When_Decoded_Then_ValuesMatch()
        {
            byte[] Text(string value)
            {
                var word = new byte[32];
                Encoding.ASCII.GetBytes(value).CopyTo(word, 0);
                return word;
            }

            var payload = Join(new[]
            {
                Word(0x40), Word(0x140),
                Word(2), Word(0x40), Word(0xa0),
                Word(2), Word(1), Word(2),
                Word(1), Word(3),
                Word(3), Word(0x60), Word(0xa0), Word(0xe0),
                Word(3), Text("one"), Word(3), Text("two"), Word(5), Text("three")
            });
            var parameters = new[] { new Parameter("uint256[][]", "a", 1), new Parameter("string[]", "b", 2) };

            var result = new ParameterDecoder().DecodeDefaultData(payload, parameters);

            var nested = ((IEnumerable)result[0].Result).Cast<IEnumerable>()
                .Select(inner => inner.Cast<object>().Select(o => Convert.ToInt32(o.ToString())).ToArray()).ToArray();
            Assert.Equal(new[] { 1, 2 }, nested[0]);
            Assert.Equal(new[] { 3 }, nested[1]);
            Assert.Equal(new[] { "one", "two", "three" }, ((IEnumerable)result[1].Result).Cast<string>().ToArray());
        }

        [Fact]
        [Trait("Rule", "S14-R3-2")]
        public void Given_ValidEncodingsOfEmptyDynamicValuesInsideArraysAndTuples_When_Decoded_Then_NothingIsRejected()
        {
            var parameters = new[]
            {
                new Parameter("bytes[]", "a", 1), new Parameter("string[]", "b", 2),
                new Parameter("uint256[][]", "c", 3), new Parameter("bytes", "d", 4)
            };
            var encoded = new ParametersEncoder().EncodeParameters(parameters,
                new object[] { new object[] { new byte[0], new byte[0], new byte[0] }, new object[] { "", "", "" },
                    new object[] { new object[0], new object[0] }, new byte[0] });

            var result = new ParameterDecoder().DecodeDefaultData(encoded, parameters.Select(p => new Parameter(p.Type, p.Name, p.Order)).ToArray());

            Assert.Equal(3, ((IList)result[0].Result).Count);
            Assert.Equal(3, ((IList)result[1].Result).Count);
            Assert.Equal(2, ((IList)result[2].Result).Count);
        }

        [Fact]
        [Trait("Rule", "S14-R3-3")]
        public void Given_AFunctionSignatureWithLongWhitespaceAfterTheClosingParenthesis_When_Deserialised_Then_ParsingStaysNearLinear()
        {
            var signature = "function f()" + Repeat(" ", 40_000) + "x";
            var clock = Stopwatch.StartNew();

            Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(signature));

            Assert.True(clock.ElapsedMilliseconds < 10000, $"{signature.Length} char signature took {clock.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData(100_000)]
        [Trait("Rule", "S14-R3-4")]
        public void Given_VeryWideSignaturesOfEveryShape_When_Deserialised_Then_ParsingStaysLinear(int width)
        {
            var shapes = new[]
            {
                "function f(" + Repeat("uint256 a,", width) + "uint256)",
                "function f((" + Repeat("uint256,", width) + "uint256))",
                "function f(" + Repeat("(", 30) + Repeat("uint256,", width) + "uint256" + Repeat(")", 30) + ")",
                "function f(" + Repeat("(uint256,uint256)[] x,", width / 4) + "uint256)",
                "event e(" + Repeat("uint256 indexed a,", width) + "uint256 b)",
                "function f(uint256" + Repeat(" ", 400_000) + "a)"
            };

            foreach (var shape in shapes)
            {
                var clock = Stopwatch.StartNew();
                Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(shape));
                Assert.True(clock.ElapsedMilliseconds < 30000, $"{shape.Length} chars took {clock.ElapsedMilliseconds} ms");
            }
        }

        [Fact]
        [Trait("Rule", "S14-R3-4")]
        public void Given_RandomUnbalancedSignatureText_When_Deserialised_Then_OnlyParseExceptionsAreRaisedAndEachCallIsFast()
        {
            var random = new Random(30);
            var alphabet = "()[],  uint256tuple\t\nindexed,)(".ToCharArray();
            var unexpected = new HashSet<string>();
            for (var i = 0; i < 20_000; i++)
            {
                var text = new StringBuilder("function f(");
                for (var j = random.Next(1, 200); j > 0; j--) text.Append(alphabet[random.Next(alphabet.Length)]);
                text.Append(')');
                var clock = Stopwatch.StartNew();
                var thrown = Record.Exception(() => ABIDeserialiserFactory.DeserialiseContractABI(text.ToString()));
                Assert.True(clock.ElapsedMilliseconds < 5000);
                for (var inner = thrown; inner != null; inner = inner.InnerException)
                    if (!(inner is FormatException || inner is ArgumentException)) unexpected.Add(inner.GetType().Name);
            }
            Assert.Empty(unexpected.Where(n => n != "ArgumentOutOfRangeException"));
        }
    }
}
