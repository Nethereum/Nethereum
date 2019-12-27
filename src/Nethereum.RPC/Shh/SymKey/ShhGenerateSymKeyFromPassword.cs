using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.SymKey
{
    public class ShhGenerateSymKeyFromPassword : GenericRpcRequestResponseHandlerParamString<string>, IShhGenerateSymKeyFromPassword
    {
        public ShhGenerateSymKeyFromPassword(IClient client) : base(client, ApiMethods.shh_generateSymKeyFromPassword.ToString())
        {
        }
    }
}
