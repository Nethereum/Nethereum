using System;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class NodeRlpDecoderEmptyKeyTests
    {
        [Fact]
        public void Given_ATrieNodeWhoseKeyRlpIsEmpty_When_Decoded_Then_TheSameExceptionTypeAsBeforeIsThrown()
        {
            var twoItemListWithEmptyKey = new byte[] { 0xC2, 0x80, 0x01 };

            Assert.Throws<NullReferenceException>(
                () => NodeRlpDecoder.Decode(twoItemListWithEmptyKey, null, new byte[0], Sha3KeccackHashProvider.Instance));
        }
    }
}
