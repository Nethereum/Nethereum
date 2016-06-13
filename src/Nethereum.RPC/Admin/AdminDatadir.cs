using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;

namespace Nethereum.RPC
{

    ///<Summary>
    /// The datadir administrative property can be queried for the absolute path the running Geth node currently uses to store all its databases.    
    ///</Summary>
    public class AdminDatadir : GenericRpcRequestResponseHandlerNoParam<string>
    {
            public AdminDatadir(IClient client) : base(client, ApiMethods.admin_datadir.ToString()) { }
    }

}
            
        