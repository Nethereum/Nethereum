using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.HostWallet
{
    public interface IWalletWatchAsset
    {
        RpcRequest BuildRequest(WatchAssetParameter watchAssetParameter, object id = null);
        Task<bool> SendRequestAsync(WatchAssetParameter watchAssetParameter, object id = null);
    }
}