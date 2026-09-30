using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Freezer;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class FreezerCodecSetTests
    {
        [Fact]
        public void Given_FreezerCodecSet_When_Constructed_Then_NoMemberIsSnappyWrapped()
        {
            var codecs = new FreezerCodecSet();

            Assert.IsType<HeaderItemCodec>(codecs.Headers);
            Assert.IsType<BodyClusterItemCodec>(codecs.Bodies);
            Assert.IsType<ReceiptsItemCodec>(codecs.Receipts);
            Assert.IsType<HashesItemCodec>(codecs.Hashes);
            Assert.IsType<BalItemCodec>(codecs.Bals);
        }
    }
}
