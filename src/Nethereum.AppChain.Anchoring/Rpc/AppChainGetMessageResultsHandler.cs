using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Anchoring.Rpc
{
    public class AppChainGetMessageResultsHandler : RpcHandlerBase
    {
        public override string MethodName => "appchain_getMessageResults";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var sourceChainIdHex = GetParam<string>(request, 0);
            var fromIndex = (int)GetOptionalParam<long>(request, 1, 0);
            var count = (int)GetOptionalParam<long>(request, 2, 100);

            if (string.IsNullOrEmpty(sourceChainIdHex))
                return Error(request.Id, -32602, "sourceChainId is required");

            if (fromIndex < 0) fromIndex = 0;
            if (count > 1000) count = 1000;
            if (count < 1) count = 1;

            BigInteger sourceChainIdBig;
            try
            {
                sourceChainIdBig = sourceChainIdHex.HexToBigInteger(false);
            }
            catch (Exception)
            {
                return Error(request.Id, -32602, "Invalid hex value for sourceChainId");
            }

            if (sourceChainIdBig < 0 || sourceChainIdBig > ulong.MaxValue)
                return Error(request.Id, -32602, "sourceChainId out of valid range");

            var sourceChainId = (ulong)sourceChainIdBig;

            var resultStore = context.GetService<IMessageResultStore>();
            if (resultStore == null)
                return Error(request.Id, -32603, "Message result store not available");

            var totalCount = await resultStore.GetCountAsync(sourceChainId);
            var pageResults = await resultStore.GetBySourceChainAsync(sourceChainId, fromIndex, count);

            var items = new List<Dictionary<string, object>>();
            foreach (var r in pageResults)
            {
                items.Add(new Dictionary<string, object>
                {
                    ["sourceChainId"] = ToHex(r.SourceChainId),
                    ["messageId"] = ToHex(r.MessageId),
                    ["leafIndex"] = ToHex(r.LeafIndex),
                    ["txHash"] = r.TxHash.ToHex(true),
                    ["success"] = r.Success,
                    ["dataHash"] = r.DataHash.ToHex(true)
                });
            }

            var accumulator = context.GetService<IMessageMerkleAccumulator>();
            if (accumulator == null)
                return Error(request.Id, -32603, "Message accumulator not available");

            var root = accumulator.GetRoot(sourceChainId);
            var lastProcessedId = accumulator.GetLastProcessedMessageId(sourceChainId);

            var response = new Dictionary<string, object>
            {
                ["sourceChainId"] = ToHex(sourceChainId),
                ["totalCount"] = ToHex(totalCount),
                ["merkleRoot"] = root.Length > 0 ? root.ToHex(true) : "0x",
                ["processedUpToMessageId"] = ToHex(lastProcessedId),
                ["results"] = items
            };

            return Success(request.Id, response);
        }
    }
}
