using System;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxPeerPayloadDecodeAmplificationSecurityTests
    {
        private const int MaxAllocatedBytesPerPayloadByte = 16;

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AMaxSizeGetBlockHeadersFrameFilledWithEmptyLists_When_TheServerDecodesIt_Then_AllocationStaysWithinAFewTimesTheFrame()
        {
            var length = RlpxFrameReader.MaxFrameSize - 4;
            var payload = new byte[length + 4];
            payload[0] = 0xfa;
            payload[1] = (byte)(length >> 16);
            payload[2] = (byte)(length >> 8);
            payload[3] = (byte)length;
            for (var i = 4; i < payload.Length; i++) payload[i] = 0xc0;

            var before = GC.GetAllocatedBytesForCurrentThread();
            try { GetBlockHeadersMessageEncoder.Decode(payload); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated <= (long)MaxAllocatedBytesPerPayloadByte * payload.Length,
                $"decoding a {payload.Length} byte frame allocated {allocated} bytes ({allocated / payload.Length}x)");
        }
    }
}
