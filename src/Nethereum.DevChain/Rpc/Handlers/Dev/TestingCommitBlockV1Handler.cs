using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class TestingCommitBlockV1Handler : RpcHandlerBase
    {
        public override string MethodName => "testing_commitBlockV1";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var attributes = GetParam<PayloadAttributesV3>(request, 0);
            var rawTransactions = GetOptionalParam<List<string>>(request, 1, null);
            var extraData = GetOptionalParam<string>(request, 2, null);

            var node = (DevChainNode)context.Node;
            var parent = await node.Blocks.GetLatestAsync();
            var parentHash = await node.Blocks.GetHashByNumberAsync(parent.BlockNumber);

            var fromMempool = rawTransactions == null;
            IReadOnlyList<ISignedTransaction> transactions = fromMempool
                ? await node.GetPendingTransactionsAsync()
                : rawTransactions
                    .Select(raw => TransactionFactory.CreateTransaction(raw.HexToByteArray(), allowBlobNetworkWrapper: false))
                    .ToList();

            var options = TestingBlockOptions.Create(node, parent, attributes, extraData);
            var committed = await node.CommitBlockAsync(parentHash, transactions, fromMempool, options);

            return Success(request.Id, committed.BlockHash.ToHex(true));
        }
    }
}
