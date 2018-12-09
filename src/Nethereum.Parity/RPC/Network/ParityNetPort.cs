using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Network
{
    /// <Summary>
    ///     parity_netPort
    ///     Returns network port the node is listening on.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Quantity - Port number
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_netPort","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X
    ///     POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": 30303
    ///     }
    /// </Summary>
    public class ParityNetPort : GenericRpcRequestResponseHandlerNoParam<int>, IParityNetPort
    {
        public ParityNetPort(IClient client) : base(client, ApiMethods.parity_netPort.ToString())
        {
        }
    }

    public interface IParityNetPort : IGenericRpcRequestResponseHandlerNoParam<int>
    {


    }
}