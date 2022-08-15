using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using System;

namespace Nethereum.RPC.HostWallet
{
    public class WalletWatchAsset : RpcRequestResponseHandler<bool>, IWalletWatchAsset
    {
        public WalletWatchAsset() : this(null)
        {
        }

        public WalletWatchAsset(IClient client) : base(client, ApiMethods.wallet_watchAsset.ToString())
        {

        }


        public  Task<bool> SendRequestAsync(WatchAssetParameter watchAssetParameter, object id = null)
        {
            if (watchAssetParameter == null) throw new ArgumentNullException(nameof(watchAssetParameter));
            if (watchAssetParameter.Type == null) throw new ArgumentNullException(nameof(watchAssetParameter.Type));
            if (watchAssetParameter.Options == null) throw new ArgumentNullException(nameof(watchAssetParameter.Options));
            if (watchAssetParameter.Options.Address == null) throw new ArgumentNullException(nameof(watchAssetParameter.Options.Address));

            return  base.SendRequestAsync(id, watchAssetParameter);

            
        }

        public RpcRequest BuildRequest(WatchAssetParameter watchAssetParameter, object id = null)
        {
            if (watchAssetParameter == null) throw new ArgumentNullException(nameof(watchAssetParameter));
            if (watchAssetParameter.Type == null) throw new ArgumentNullException(nameof(watchAssetParameter.Type));
            if (watchAssetParameter.Options == null) throw new ArgumentNullException(nameof(watchAssetParameter.Options));
            if (watchAssetParameter.Options.Address == null) throw new ArgumentNullException(nameof(watchAssetParameter.Options.Address));

            return base.BuildRequest(id, watchAssetParameter);
        }
    }
}
