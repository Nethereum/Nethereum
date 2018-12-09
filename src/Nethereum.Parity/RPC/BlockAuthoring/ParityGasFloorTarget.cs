using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.BlockAuthoring
{
    /// <Summary>
    ///     parity_gasFloorTarget
    ///     Returns current target for gas floor.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Quantity - Gas floor target.
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_gasFloorTarget","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x47b760" // 4700000
    ///     }
    /// </Summary>
    public class ParityGasFloorTarget : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>, IParityGasFloorTarget
    {
        public ParityGasFloorTarget(IClient client) : base(client, ApiMethods.parity_gasFloorTarget.ToString())
        {
        }
    }

    public interface IParityGasFloorTarget : IGenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {


    }
}