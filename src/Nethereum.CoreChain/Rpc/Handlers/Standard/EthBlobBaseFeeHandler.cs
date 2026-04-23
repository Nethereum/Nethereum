using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthBlobBaseFeeHandler : RpcHandlerBase
    {
        public override string MethodName => "eth_blobBaseFee";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var latestBlock = await context.Node.GetLatestBlockAsync();
            if (latestBlock == null)
                return Error(request.Id, -32000, "No blocks available");

            var excessBlobGas = latestBlock.ExcessBlobGas ?? 0;
            var blobBaseFee = BlobGasCalculator.CalculateBlobBaseFee((EvmUInt256)(ulong)excessBlobGas);

            return Success(request.Id, "0x" + ((System.Numerics.BigInteger)blobBaseFee).ToString("x"));
        }
    }
}
