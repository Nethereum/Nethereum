using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugGetRawBlockHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.debug_getRawBlock.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var header = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (header == null)
                return Success(request.Id, null);

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);
            var transactions = await context.Node.Transactions.GetByBlockHashAsync(blockHash);
            var uncles = context.Node.Uncles != null
                ? await context.Node.Uncles.GetByBlockHashAsync(blockHash)
                : null;
            var withdrawals = await WithdrawalsLoader.LoadAsync(context, header, blockHash);

            var encoded = NewBlockMessageEncoder.EncodeBlock(
                header,
                transactions ?? new List<ISignedTransaction>(),
                uncles ?? new List<BlockHeader>(),
                withdrawals);

            return Success(request.Id, encoded.ToHex(true));
        }
    }
}
