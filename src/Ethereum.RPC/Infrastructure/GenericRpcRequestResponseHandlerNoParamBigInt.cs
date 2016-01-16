using System.Numerics;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using RPCRequestResponseHandlers;

namespace Ethereum.RPC
{
    public class GenericRpcRequestResponseHandlerNoParamBigInt: GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
        public GenericRpcRequestResponseHandlerNoParamBigInt(string methodName) : base(methodName)
        {
        }

        
    }
}