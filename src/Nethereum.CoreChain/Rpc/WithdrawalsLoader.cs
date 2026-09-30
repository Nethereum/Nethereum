using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Rpc
{
    internal static class WithdrawalsLoader
    {
        public static async Task<IList<Withdrawal>> LoadAsync(RpcContext context, BlockHeader header, byte[] blockHash)
        {
            if (header.WithdrawalsRoot == null) return null;

            var withdrawalStore = context.GetService<Storage.IChainStoreBundle>()?.Withdrawals;
            return withdrawalStore != null
                ? await withdrawalStore.GetByBlockHashAsync(blockHash)
                : null;
        }
    }
}
