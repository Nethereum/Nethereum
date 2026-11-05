using System.Linq;
using Nethereum.Merkle.Patricia.Proofs;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class NibblesToCompactTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Given_EvenAndOddNibbleKeys_When_NibblesToCompactWithLeafFlagIsRoundTrippedThroughCompactToNibbles_Then_TheNibblesAreRecovered(bool leaf)
        {
            for (var length = 0; length <= 9; length++)
            {
                var nibbles = Enumerable.Range(0, length).Select(i => (byte)((i * 7 + 3) & 0x0F)).ToArray();

                var compact = PatriciaPathWalker.NibblesToCompact(nibbles, leaf);

                var expectedFlag = (leaf ? 2 : 0) + (length % 2 == 1 ? 1 : 0);
                Assert.Equal(expectedFlag, compact[0] >> 4);
                Assert.Equal(1 + length / 2, compact.Length);
                Assert.Equal(nibbles, PatriciaPathWalker.CompactToNibbles(compact));
            }
        }

        [Fact]
        public void Given_AnEmptyNibblePath_When_CompactEncoded_Then_LeafGives0x20AndExtensionGives0x00()
        {
            Assert.Equal(new byte[] { 0x20 }, PatriciaPathWalker.NibblesToCompact(new byte[0], true));
            Assert.Equal(new byte[] { 0x00 }, PatriciaPathWalker.NibblesToCompact(new byte[0], false));
            Assert.Equal(new byte[] { 0x20 }, PatriciaPathWalker.NibblesToCompact(null, true));
            Assert.Equal(new byte[] { 0x00 }, PatriciaPathWalker.NibblesToCompact(null, false));
        }

        [Fact]
        public void Given_NibblesWithoutALeafFlag_When_CompactEncodedByTheOneArgumentOverload_Then_ItEqualsTheExtensionEncoding()
        {
            for (var length = 0; length <= 6; length++)
            {
                var nibbles = Enumerable.Range(0, length).Select(i => (byte)(i + 1)).ToArray();
                Assert.Equal(PatriciaPathWalker.NibblesToCompact(nibbles, false), PatriciaPathWalker.NibblesToCompact(nibbles));
            }
        }
    }
}
