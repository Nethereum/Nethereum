using System.Numerics;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthSimulateV1Handler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_simulateV1.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var input = GetParam<EthSimulateInput>(request, 0);

            var blockTag = GetOptionalParam<string>(request, 1, null);
            BigInteger? baseBlockNumber = null;
            if (!string.IsNullOrEmpty(blockTag) &&
                !string.Equals(blockTag, "latest") &&
                !string.Equals(blockTag, "pending"))
            {
                baseBlockNumber = await ResolveBlockNumberAsync(blockTag, context);
            }

            var response = await context.Node.SimulateAsync(input, baseBlockNumber);

            return Success(request.Id, response);
        }
    }
}
