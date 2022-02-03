
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Retrieves the public address that is used to sign proposals, which is derived from the node’s nodekey.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: string - node’s public signing address    
///</Summary>
    public class IstanbulNodeAddress : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public IstanbulNodeAddress(IClient client) : base(client, ApiMethods.istanbul_nodeAddress.ToString()) { }
    }

}
