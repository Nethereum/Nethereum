using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class TestingBuildBlockV1Handler : RpcHandlerBase
    {
        public override string MethodName => "testing_buildBlockV1";


        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var parentHash = GetParam<string>(request, 0).HexToByteArray();
            var attributes = GetParam<PayloadAttributesV3>(request, 1);
            var rawTransactions = GetOptionalParam<List<string>>(request, 2, null);
            var extraData = GetOptionalParam<string>(request, 3, null);

            var node = (DevChainNode)context.Node;
            var parent = await node.Blocks.GetLatestAsync();

            var orderLikeMempool = rawTransactions == null;
            IReadOnlyList<ISignedTransaction> transactions = orderLikeMempool
                ? await node.GetPendingTransactionsAsync()
                : rawTransactions
                    .Select(raw => TransactionFactory.CreateTransaction(raw.HexToByteArray(), allowBlobNetworkWrapper: false))
                    .ToList();

            var options = TestingBlockOptions.Create(node, parent, attributes, extraData);
            var withdrawals = EnginePayloadDecoder.ToModelWithdrawals(attributes.Withdrawals);

            var built = await node.BuildBlockOnHeadAsync(parentHash, transactions, orderLikeMempool, options);

            return Success(request.Id, EngineApiService.ToGetPayloadV4Response(built, withdrawals, BlockValue(built)));
        }

        private static BigInteger BlockValue(BlockProductionResult built)
        {
            var baseFee = built.Header.BaseFee ?? EvmUInt256.Zero;
            var value = BigInteger.Zero;
            for (var i = 0; i < built.IncludedTransactions.Count; i++)
            {
                var tip = (built.IncludedTransactions[i].GetEffectiveGasPrice(baseFee) - baseFee).ToBigInteger();
                value += tip * built.TransactionResults[i].GasUsed;
            }
            return value;
        }
    }
}
