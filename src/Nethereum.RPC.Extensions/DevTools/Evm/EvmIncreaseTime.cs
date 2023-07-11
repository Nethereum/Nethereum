
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Jump forward in time by the given amount of time, in seconds.    
///</Summary>
    public class EvmIncreaseTime : RpcRequestResponseHandler<int>
    {
        public EvmIncreaseTime(IClient client) : base(client,ApiMethods.evm_increaseTime.ToString()) { }

        public Task<int> SendRequestAsync(int seconds, object id = null)
        {
            return base.SendRequestAsync(id, seconds);
        }
        public RpcRequest BuildRequest(int seconds, object id = null)
        {
            return base.BuildRequest(id, seconds);
        }
    }

}

