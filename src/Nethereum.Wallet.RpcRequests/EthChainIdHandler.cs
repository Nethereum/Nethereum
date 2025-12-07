using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthChainIdHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_chainId";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var chainIdHex = context.ChainId?.HexValue ?? "0x1";
            return Task.FromResult(new RpcResponseMessage(request.Id, chainIdHex));
        }
    }

}
