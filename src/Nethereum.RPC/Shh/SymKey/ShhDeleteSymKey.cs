using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.SymKey
{
    public class ShhDeleteSymKey : GenericRpcRequestResponseHandlerParamString<bool>, IShhDeleteSymKey
    {
        public ShhDeleteSymKey(IClient client) : base(client, ApiMethods.shh_deleteSymKey.ToString())
        {
        }
    }
}
