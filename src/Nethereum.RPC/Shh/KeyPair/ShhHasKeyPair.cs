using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.Shh.KeyPair;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.KeyPair
{
    public class ShhHasKeyPair : GenericRpcRequestResponseHandlerParamString<bool>, IShhHasKeyPair
    {
        public ShhHasKeyPair(IClient client) : base(client, ApiMethods.shh_hasKeyPair.ToString())
        {
        } 
    }
}