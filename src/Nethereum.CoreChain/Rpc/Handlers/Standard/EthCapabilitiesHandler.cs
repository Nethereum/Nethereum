using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCapabilitiesHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_capabilities.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var head = await context.Node.GetLatestBlockAsync();
            var headNumber = head != null ? (BigInteger)head.BlockNumber : BigInteger.Zero;
            var headHash = await context.Node.GetBlockHashByNumberAsync(headNumber);

            var response = new EthCapabilitiesResult
            {
                Head = new EthCapabilitiesHead
                {
                    Hash = headHash.ToHex(true),
                    Number = new HexBigInteger(headNumber)
                },
                Blocks = FullyAvailableResource(),
                Logs = FullyAvailableResource(),
                Receipts = FullyAvailableResource(),
                State = FullyAvailableResource(),
                Stateproofs = FullyAvailableResource(),
                Tx = FullyAvailableResource()
            };

            return Success(request.Id, response);
        }

        private static EthCapabilitiesEffectiveResource FullyAvailableResource() => new EthCapabilitiesEffectiveResource
        {
            Disabled = false,
            OldestBlock = new HexBigInteger(0)
        };
    }
}
