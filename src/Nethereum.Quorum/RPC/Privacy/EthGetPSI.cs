
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// When using multiple private states, returns the private state the user is operating on.
    /// 
    /// Parameters
    /// None
    /// 
    /// Returns
    /// result: string - the private state identifier (PSI)    
    ///</Summary>
    public interface IEthGetPSI
    {
        Task<String> SendRequestAsync(object id);
        RpcRequest BuildRequest(object id = null);
    }

    ///<Summary>
/// When using multiple private states, returns the private state the user is operating on.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: string - the private state identifier (PSI)    
///</Summary>
    public class EthGetPSI : GenericRpcRequestResponseHandlerNoParam<string>, IEthGetPSI
    {
        public EthGetPSI(IClient client) : base(client, ApiMethods.eth_getPSI.ToString()) { }
    }

}
