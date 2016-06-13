

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
       /// The setSolc administrative method sets the Solidity compiler path to be used by the node when invoking the eth_compileSolidity RPC method. The Solidity compiler path defaults to /usr/bin/solc if not set, so you only need to change it for using a non-standard compiler location.
/// 
/// The method accepts an absolute path to the Solidity compiler to use (specifying a relative path would depend on the current – to the user unknown – working directory of Geth), and returns the version string reported by solc --version.    
    ///</Summary>
    public class AdminSetSolc : RpcRequestResponseHandler<string>
        {
            public AdminSetSolc(RpcClient client) : base(client,ApiMethods.admin_setSolc.ToString()) { }

            public Task<string> SendRequestAsync(string path, object id = null)
            {
                return base.SendRequestAsync(id, path);
            }
            public RpcRequest BuildRequest(string path, object id = null)
            {
                return base.BuildRequest(id, path);
            }
        }

    }

