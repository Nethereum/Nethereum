using System;
using System.Diagnostics;
using Nethereum.ABI.ABIDeserialisation;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14.Round3
{
    [Trait("Category", "TimingSensitive")]
    public class AbiSignatureLinearityS14Tests
    {
        private const int Size = 100_000;
        private const long BoundMilliseconds = 5000;

        public static TheoryData<string, string, string> Shapes()
        {
            var data = new TheoryData<string, string, string>();
            foreach (var fillerName in new[] { "spaces", "word" })
            {
                var filler = fillerName == "spaces" ? new string(' ', Size) : new string('a', Size);

                data.Add(fillerName + "-after-paren", "function f(uint256 a)" + filler + "view", "uint256 a");
                data.Add(fillerName + "-after-paren-no-keyword", "function f(uint256 a)" + filler, "uint256 a");
                data.Add(fillerName + "-before-returns", "function f(uint256 a) view" + filler + "returns (uint256)", "uint256 a");
                data.Add(fillerName + "-around-returns", "function f(uint256 a)" + filler + "returns" + filler + "(uint256)", "uint256 a");
                var between = "uint256 a," + filler + "uint256 b";
                data.Add(fillerName + "-between-params", "function f(" + between + ")", between);
                var indexed = "uint256 indexed" + filler + "a";
                data.Add(fillerName + "-after-indexed", "event E(" + indexed + ")", indexed);
                data.Add(fillerName + "-before-open-paren", "function" + filler + "f" + filler + "(uint256 a)", "uint256 a");
                data.Add(fillerName + "-repeated-parens", "function f(" + new string('(', 30) + new string(')', 30) + ")" + filler, "uint256 a");
            }
            return data;
        }

        private static void AssertFast(string shape, Action action)
        {
            var clock = Stopwatch.StartNew();
            Record.Exception(action);
            Assert.True(clock.ElapsedMilliseconds < BoundMilliseconds, $"{shape} took {clock.ElapsedMilliseconds} ms");
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        [Trait("Rule", "S14-R3-1")]
        public void Given_HundredThousandCharacterFillerInASignature_When_EveryEntryPointParsesIt_Then_EachFinishesQuickly(
            string shape, string signature, string parameters)
        {
            var parser = new ABIStringSignatureDeserialiser();

            AssertFast(shape + " ExtractFunctionABI", () => parser.ExtractFunctionABI(signature, "f", parameters));
            AssertFast(shape + " ExtractEventABI", () => parser.ExtractEventABI(signature, "E", parameters));
            AssertFast(shape + " ExtractErrorABI", () => parser.ExtractErrorABI(signature, "R", parameters));
            AssertFast(shape + " ExtractConstructorABI", () => parser.ExtractConstructorABI(signature, "", parameters));
            AssertFast(shape + " ExtractContractABI", () => parser.ExtractContractABI(signature));
            AssertFast(shape + " DeserialiseContractABI", () => ABIDeserialiserFactory.DeserialiseContractABI(signature));
            AssertFast(shape + " DeserialiseContractABI event", () => ABIDeserialiserFactory.DeserialiseContractABI("event E(" + parameters + ")"));
            AssertFast(shape + " DeserialiseContractABI error", () => ABIDeserialiserFactory.DeserialiseContractABI("error R(" + parameters + ")"));
            AssertFast(shape + " DeserialiseContractABI constructor", () => ABIDeserialiserFactory.DeserialiseContractABI("constructor(" + parameters + ")"));
        }

        [Fact]
        [Trait("Rule", "S14-R3-1")]
        public void Given_ManyBalancedAndUnbalancedParentheses_When_Parsed_Then_ItFinishesQuickly()
        {
            var signature = "function f(" + string.Concat(System.Linq.Enumerable.Repeat("(uint256),", 20_000)) + "uint256) view";
            var unbalanced = "function f" + string.Concat(System.Linq.Enumerable.Repeat(")(", 40_000));

            AssertFast("balanced", () => ABIDeserialiserFactory.DeserialiseContractABI(signature));
            AssertFast("unbalanced", () => ABIDeserialiserFactory.DeserialiseContractABI(unbalanced));
        }
    }
}
