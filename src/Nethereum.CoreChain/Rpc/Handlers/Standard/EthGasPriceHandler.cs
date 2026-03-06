using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGasPriceHandler : RpcHandlerBase
    {
        private const long DefaultPriorityFeePerGas = 1_000_000_000;

        public override string MethodName => ApiMethods.eth_gasPrice.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var baseFee = context.Node.Config.BaseFee;
            var gasPrice = baseFee + DefaultPriorityFeePerGas;
            return Task.FromResult(Success(request.Id, new HexBigInteger(gasPrice)));
        }
    }
}
