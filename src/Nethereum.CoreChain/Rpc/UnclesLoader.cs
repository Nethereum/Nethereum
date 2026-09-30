using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Rpc
{
    internal static class UnclesLoader
    {
        public static async Task<IList<BlockHeader>> LoadAsync(RpcContext context, byte[] blockHash)
        {
            var uncleStore = context.Node.Uncles;
            return uncleStore != null
                ? await uncleStore.GetByBlockHashAsync(blockHash)
                : null;
        }
    }
}
