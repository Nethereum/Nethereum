using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Eth.Compilation
{
    /// <Summary>
    ///     eth_compileLLL
    ///     Returns compiled LLL code.
    ///     Parameters
    ///     String - The source code.
    ///     params: [
    ///     "(returnlll (suicide (caller)))",
    ///     ]
    ///     Returns
    ///     DATA - The compiled source code.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_compileSolidity","params":["(returnlll (suicide
    ///     (caller)))"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result":
    ///     "0x603880600c6000396000f3006001600060e060020a600035048063c6888fa114601857005b6021600435602b565b8060005260206000f35b600081600702905091905056"
    ///     // the compiled source code
    ///     }
    /// </Summary>
    public class EthCompileLLL : RpcRequestResponseHandler<JObject>, IEthCompileLLL
    {
        public EthCompileLLL(IClient client) : base(client, ApiMethods.eth_compileLLL.ToString())
        {
        }

        public Task<JObject> SendRequestAsync(string lllcode, object id = null)
        {
            if (lllcode == null) throw new ArgumentNullException(nameof(lllcode));
            return base.SendRequestAsync(id, lllcode);
        }

        public RpcRequest BuildRequest(string lllcode, object id = null)
        {
            if (lllcode == null) throw new ArgumentNullException(nameof(lllcode));
            return base.BuildRequest(id, lllcode);
        }
    }
}