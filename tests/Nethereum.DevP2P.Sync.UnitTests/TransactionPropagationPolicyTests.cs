using Nethereum.DevP2P.Sync.Mempool;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TransactionPropagationPolicyTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(8, 2)]
        [InlineData(9, 3)]
        [InlineData(16, 4)]
        [InlineData(100, 10)]
        public void DirectPeerCount_IsFloorSqrtOfPeerCount(int peerCount, int expectedDirect)
        {
            Assert.Equal(expectedDirect, TransactionPropagationPolicy.DirectPeerCount(peerCount));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(50)]
        public void DirectPeerCount_NeverExceedsPeerCount_AndLeavesTheRestToAnnounce(int peerCount)
        {
            var direct = TransactionPropagationPolicy.DirectPeerCount(peerCount);
            Assert.InRange(direct, 1, peerCount);
            Assert.True(peerCount - direct >= 0);
        }
    }
}
