using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class HardhatSetBalanceHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.hardhat_setBalance.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var address = GetParam<string>(request, 0);
            var balanceHex = GetParam<string>(request, 1);
            var balance = balanceHex.HexToBigInteger(false);

            await devNode.SetBalanceAsync(address, balance);

            return Success(request.Id, true);
        }
    }
}
