using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S14.Round4
{
    [Trait("Category", "TimingSensitive")]
    public class AbiRound4S14Tests
    {
        private const string Bzzr0First = "6060604052341561000f57600080fd5b60ac8061001d6000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416635170a9d081146043575b600080fd5b3415604d57600080fd5b6053607c565b60405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390f35b33905600a165627a7a72305820ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c320029";
        private const string Bzzr0Second = "6060604052341561000f57600080fd5b5b60bd8061001e6000396000f300606060405263ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166332b7a7618114603c575b600080fd5b3415604657600080fd5b604c604e565b005b600460027f07392b89121f4d481601a3db92f2daf8c73cc086a942b4522c58abcbdeac4d016001600360405191825260208201526040908101905180910390a35b5600a165627a7a723058202af7567cbbe622cac1fcce7d7f4aa0be6c879974474c7f7580a9fea9d4dfa5850029";
        private const string IpfsMetadata = "9063677342ce90602401602060405180830381865af415801560a0573d6000803e3d6000fd5b505050506040513d601f19601f8201168201806040525081019060c2919060e0565b92915050565b60006020828403121560d957600080fd5b5035919050565b60006020828403121560f157600080fd5b505191905056fea264697066735822122041bec357755b8846eded76c806a9e0bceffa6b70e8e4e4e594aaeb5f924d22d364736f6c63430008130033";

        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        private static void AssertFast(Action action, string what, int limitMs = 15000)
        {
            var clock = Stopwatch.StartNew();
            Record.Exception(action);
            Assert.True(clock.ElapsedMilliseconds < limitMs, $"{what} took {clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R4-1")]
        public void Given_ManyClosingParenthesesSeparatedByWhitespaceAndWordRuns_When_Deserialised_Then_ParsingStaysLinear()
        {
            var runs = "function f(uint256)" + Repeat(")   a\t  )b  ", 30_000) + "x";
            var withModifier = "function f()" + Repeat(")  ab  ", 30_000) + " view";
            var spacesOnly = "function f()" + Repeat(")" + new string(' ', 50), 8_000) + "x";

            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(runs), "closing parens with runs");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(withModifier), "closing parens then modifier");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(spacesOnly), "closing parens then spaces");
        }

        [Fact]
        [Trait("Rule", "S14-R4-1")]
        public void Given_ManyReturnsKeywordsAndUnmatchedParentheses_When_Deserialised_Then_ParsingStaysLinear()
        {
            var returnsRepeated = "function f() " + Repeat("returns  ", 40_000) + "x";
            var returnsSpaces = "function f() " + Repeat("returns" + new string(' ', 20), 10_000) + "(";
            var unmatchedOpeners = "function f" + Repeat("(", 32) + Repeat("a(", 40_000);
            var unmatchedClosers = "function f(" + Repeat(")", 100_000);
            var alternating = "function " + Repeat("a (", 20_000) + Repeat(") b", 20_000);

            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(returnsRepeated), "returns repeated");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(returnsSpaces), "returns then spaces");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(unmatchedOpeners), "unmatched openers");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(unmatchedClosers), "unmatched closers");
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(alternating), "alternating");
        }

        [Fact]
        [Trait("Rule", "S14-R4-1")]
        public void Given_ManyLinesEachWithWhitespaceHeavyHeads_When_Deserialised_Then_ParsingStaysLinear()
        {
            var lines = Repeat("  function  \t  f ( uint256   a ,  bytes b )   view  returns ( bool ok )   \n", 5_000);
            var wideTupleArrays = "function f(" + Repeat("(uint256,(bool,bytes))[] a,", 3_000) + "uint256)";

            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(lines), "many lines", 30000);
            AssertFast(() => ABIDeserialiserFactory.DeserialiseContractABI(wideTupleArrays), "wide tuple arrays", 30000);
        }

        [Theory]
        [InlineData(Bzzr0First, "ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c32")]
        [InlineData(Bzzr0Second, "2af7567cbbe622cac1fcce7d7f4aa0be6c879974474c7f7580a9fea9d4dfa585")]
        [InlineData(IpfsMetadata, null)]
        [Trait("Rule", "S14-R4-2")]
        public void Given_RealSolcBytecode_When_TheSwarmAddressIsExtracted_Then_ItMatchesTheValueTheRegexImplementationReturned(string bytecode, string expected)
        {
            var extractor = new ByteCodeSwarmExtractor();

            Assert.Equal(expected, extractor.GetSwarmAddress(bytecode));
            Assert.Equal(expected, extractor.GetSwarmAddress("0x" + bytecode));
            Assert.Equal(expected != null, extractor.HasSwarmAddress(bytecode));
        }

        [Fact]
        [Trait("Rule", "S14-R4-2")]
        public void Given_Bzzr1AndIpfsMetadataVariants_When_TheSwarmAddressIsExtracted_Then_NoneIsReportedAsABzzr0Address()
        {
            var extractor = new ByteCodeSwarmExtractor();
            var bzzr1 = "6080604052" + "a265627a7a72315820" + new string('c', 64) + "64736f6c63430005110032";
            var ipfs = "6080604052" + "a2646970667358221220" + new string('d', 64) + "64736f6c63430008110033";

            Assert.Null(extractor.GetSwarmAddress(bzzr1));
            Assert.Null(extractor.GetSwarmAddress(ipfs));
        }

        [Fact]
        [Trait("Rule", "S14-R4-2")]
        public void Given_BytecodeWithManyBzzr0PrefixesAndNoEnding_When_TheSwarmAddressIsExtracted_Then_ItStaysLinear()
        {
            var prefix = "a165627a7a72305820";
            var manyPrefixes = Repeat(prefix, 40_000) + new string('a', 64);
            var prefixesWithHex = Repeat(prefix + new string('b', 64), 8_000);
            var manyEndings = prefix + new string('e', 64) + Repeat("0029", 100_000);
            var manyLines = Repeat(prefix + new string('1', 70) + "\n", 20_000);
            var extractor = new ByteCodeSwarmExtractor();

            AssertFast(() => extractor.GetSwarmAddress(manyPrefixes), "prefixes without ending");
            AssertFast(() => extractor.GetSwarmAddress(prefixesWithHex), "prefixes with hex");
            AssertFast(() => extractor.GetSwarmAddress(manyEndings), "many endings");
            AssertFast(() => extractor.GetSwarmAddress(manyLines), "many lines");
            AssertFast(() => extractor.HasSwarmAddress(manyPrefixes), "HasSwarmAddress prefixes");
        }
    }
}
