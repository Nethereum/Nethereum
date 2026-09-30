using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.ChainNode.Hosting
{
    public static class ChainHeadResolver
    {
        public static async Task<(ulong HeadBlock, ulong HeadTime)> ResolveOurHeadAsync(IChainStoreBundle bundle)
        {
            var head = await bundle.Blocks.GetLatestAsync().ConfigureAwait(false);
            if (head != null)
                return ((ulong)head.BlockNumber, (ulong)head.Timestamp);

            var genesis = await bundle.Blocks.GetByNumberAsync(BigInteger.Zero).ConfigureAwait(false);
            return genesis == null ? (0UL, 0UL) : (0UL, (ulong)genesis.Timestamp);
        }
    }
}
