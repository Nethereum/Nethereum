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

        public static async Task<Nethereum.Model.P2P.BlockRangeUpdateMessage> ResolveBlockRangeAsync(IChainStoreBundle bundle)
        {
            var height = await bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
            var latest = height < 0 ? 0UL : (ulong)height;
            var hash = await bundle.Blocks.GetHashByNumberAsync(latest).ConfigureAwait(false);
            if (hash == null || hash.Length == 0) return null;

            return new Nethereum.Model.P2P.BlockRangeUpdateMessage
            {
                EarliestBlock = 0,
                LatestBlock = latest,
                LatestBlockHash = hash
            };
        }
    }
}
