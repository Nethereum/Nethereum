

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
 /// The startRPC administrative method starts an HTTP based JSON RPC API webserver to handle client requests. All the parameters are optional:
/// 
/// host: network interface to open the listener socket on (defaults to "localhost")
/// 
/// port: network port to open the listener socket on (defaults to 8545)
/// 
/// cors: cross-origin resource sharing header to use (defaults to "")
/// 
/// apis: API modules to offer over this interface (defaults to "eth,net,web3")
/// 
/// The method returns a boolean flag specifying whether the HTTP RPC listener was opened or not. Please note, only one HTTP endpoint is allowed to be active at any time.    
    ///</Summary>
    public class AdminStartRPC : RpcRequestResponseHandler<bool>
        {
            public AdminStartRPC(RpcClient client) : base(client,ApiMethods.admin_startRPC.ToString()) { }

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

