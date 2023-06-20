using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{
    public class EvmSetNextBlockTimestamp : RpcRequestResponseHandler<string>
    {
        public EvmSetNextBlockTimestamp(IClient client) : base(client, "evm_setNextBlockTimestamp".ToString())
        {
        }

        public Task SendRequestAsync(HexBigInteger targetTimeStamp, object id = null)
        {
           return SendRequestAsync(id, targetTimeStamp);
        }

        public RpcRequest BuildRequest(HexBigInteger targetTimeStamp, object id = null)
        {
            return BuildRequest(id, targetTimeStamp);
        }
    }

}
    
