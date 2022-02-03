
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Returns the current candidates which the node tries to vote in or out.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: map of strings to booleans - current candidates map    
///</Summary>
    public class IstanbulCandidates : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public IstanbulCandidates(IClient client) : base(client, ApiMethods.istanbul_candidates.ToString()) { }
    }

}
