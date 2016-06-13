

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
       /// The startWS administrative method starts an WebSocket based JSON RPC API webserver to handle client requests. All the parameters are optional:
/// 
/// host: network interface to open the listener socket on (defaults to "localhost")
/// port: network port to open the listener socket on (defaults to 8546)
/// cors: cross-origin resource sharing header to use (defaults to "")
/// apis: API modules to offer over this interface (defaults to "eth,net,web3")
/// The method returns a boolean flag specifying whether the WebSocket RPC listener was opened or not. Please note, only one WebSocket endpoint is allowed to be active at any time.    
    ///</Summary>
    public class AdminStartWS : RpcRequestResponseHandler<bool>
        {
            public AdminStartWS(RpcClient client) : base(client,ApiMethods.admin_startWS.ToString()) { }

            public Task<bool> SendRequestAsync(string host, HexBigInteger port, string cors, string api, object id = null)
            {
                return base.SendRequestAsync(id, host, port, cors, api);
            }
            public RpcRequest BuildRequest(string host, HexBigInteger port, string cors, string api, object id = null)
            {
                return base.BuildRequest(id, host, port, cors, api);
            }
        }

    }

