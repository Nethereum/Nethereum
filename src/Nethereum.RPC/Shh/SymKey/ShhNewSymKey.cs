using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.SymKey
{
    public class ShhNewSymKey : GenericRpcRequestResponseHandlerNoParam<string>, IShhNewSymKey
    {
        public ShhNewSymKey(IClient client) : base(client, ApiMethods.shh_newSymKey.ToString())
        {
        }
    }
}
