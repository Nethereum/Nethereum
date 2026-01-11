using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockTransactionCountByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockTransactionCountByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);

            BigInteger blockNumber;
            if (blockTag == BlockParameter.BlockParameterType.latest.ToString() || blockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                blockNumber = await context.Node.GetBlockNumberAsync();
            }
            else if (blockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                blockNumber = 0;
            }
            else
            {
                blockNumber = blockTag.HexToBigInteger(false);
            }

            var transactions = await context.Node.Transactions.GetByBlockNumberAsync(blockNumber);
            if (transactions == null)
            {
                return Success(request.Id, null);
            }

            return Success(request.Id, new HexBigInteger(transactions.Count));
        }
    }
}
