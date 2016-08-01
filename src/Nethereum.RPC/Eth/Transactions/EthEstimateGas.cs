using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_estimateGas
    ///     Makes a call or transaction, which won't be added to the blockchain and returns the used gas, which can be used for
    ///     estimating the used gas.
    ///     Parameters
    ///     See eth_call parameters, expect that all properties are optional.
    ///     Returns
    ///     QUANTITY - the amount of gas used.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_estimateGas","params":[{see above}],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x5208" // 21000
    ///     }
    /// </Summary>
    public class EthEstimateGas : RpcRequestResponseHandler<HexBigInteger>
    {
        public EthEstimateGas(IClient client) : base(client, ApiMethods.eth_estimateGas.ToString())
        {
        }

        public Task<HexBigInteger> SendRequestAsync(CallInput callInput, object id = null)
        {
            return SendRequestAsync(id, callInput);
        }

        public RpcRequest BuildRequest(CallInput callInput, object id = null)
        {
            return base.BuildRequest(id, callInput);
        }
    }
}