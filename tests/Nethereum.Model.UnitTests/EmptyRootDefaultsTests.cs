using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class EmptyRootDefaultsTests
    {
        [Fact]
        public void Given_DefaultValues_When_EmptyTrieRootAndEmptyUnclesHashAreRead_Then_TheyEqualKeccakOfRlpEmptyStringAndKeccakOfRlpEmptyList()
        {
            var keccakOfRlpEmptyString = new Sha3Keccack().CalculateHash(new byte[] { 0x80 });
            var keccakOfRlpEmptyList = new Sha3Keccack().CalculateHash(new byte[] { 0xC0 });

            Assert.Equal(keccakOfRlpEmptyString, DefaultValues.EMPTY_TRIE_HASH);
            Assert.Equal(keccakOfRlpEmptyString, DefaultValues.EMPTY_TRIE_ROOT);
            Assert.Equal(keccakOfRlpEmptyList, DefaultValues.EMPTY_UNCLES_HASH);
            Assert.Equal("56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421", DefaultValues.EMPTY_TRIE_HASH.ToHex());
            Assert.Equal("1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347", DefaultValues.EMPTY_UNCLES_HASH.ToHex());
        }
    }
}
