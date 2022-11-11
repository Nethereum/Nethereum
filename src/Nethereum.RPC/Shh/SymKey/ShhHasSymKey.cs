using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.SymKey
{
    public class ShhHasSymKey : GenericRpcRequestResponseHandlerParamString<bool>, IShhHasSymKey
    {
        public ShhHasSymKey(IClient client) : base(client, ApiMethods.shh_hasSymKey.ToString())
        {
        }
    }
}
