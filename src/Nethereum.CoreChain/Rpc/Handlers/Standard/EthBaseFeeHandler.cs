using System.Numerics;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthBaseFeeHandler : RpcHandlerBase
    {
        public override string MethodName => "eth_baseFee";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var latestBlock = await context.Node.GetLatestBlockAsync();
            if (latestBlock == null)
                return Error(request.Id, -32000, "No blocks available");

            var nextBaseFee = context.Node.Config.FeeFreeBaseFee ? BigInteger.Zero : (BigInteger)BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                latestBlock.BaseFee, latestBlock.GasLimit, latestBlock.GasUsed);

            return Success(request.Id, "0x" + nextBaseFee.ToString("x"));
        }
    }
}
