using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

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

            var blockHeader = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);

            if (includeTransactions)
            {
                return Success(request.Id, blockHeader.ToBlockWithTransactions(blockHash));
            }
            else
            {
                return Success(request.Id, blockHeader.ToBlockWithTransactionHashes(blockHash));
            }
        }
    }
}
