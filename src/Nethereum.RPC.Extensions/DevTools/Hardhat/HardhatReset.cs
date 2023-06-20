
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// .    
///</Summary>
    public class HardhatReset : RpcRequestResponseHandler<string>
    {
        public HardhatReset(IClient client) : base(client, ApiMethods.hardhat_reset.ToString()) { }

        public Task SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id);
        }

        public RpcRequest BuildRequest(object id = null)
        {
            return base.BuildRequest(id);
        }
    }

}
