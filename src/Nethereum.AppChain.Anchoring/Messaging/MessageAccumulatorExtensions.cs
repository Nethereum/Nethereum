using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public static class MessageAccumulatorExtensions
    {
        public static async Task<int> RebuildFromStoreAsync(
            this IMessageMerkleAccumulator accumulator,
            IMessageResultStore store)
        {
            int totalRebuilt = 0;
            var chainIds = await store.GetSourceChainIdsAsync().ConfigureAwait(false);

            foreach (var chainId in chainIds)
            {
                var results = await store.GetAllBySourceChainOrderedByLeafIndexAsync(chainId).ConfigureAwait(false);
                foreach (var result in results)
                {
                    accumulator.AppendLeaf(chainId, result.ToLeaf());
                    totalRebuilt++;
                }
            }

            return totalRebuilt;
        }
    }
}
