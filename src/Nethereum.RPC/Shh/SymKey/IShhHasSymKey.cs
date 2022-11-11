using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.SymKey
{
    public interface IShhHasSymKey : IGenericRpcRequestResponseHandlerParamString<bool>
    { 
    }
}
