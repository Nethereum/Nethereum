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
    public class AppChainGetMessageProofHandler : RpcHandlerBase
    {
        public override string MethodName => "appchain_getMessageProof";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var sourceChainIdHex = GetParam<string>(request, 0);
            var messageIdHex = GetParam<string>(request, 1);

            if (string.IsNullOrEmpty(sourceChainIdHex))
                return Error(request.Id, -32602, "sourceChainId is required");
            if (string.IsNullOrEmpty(messageIdHex))
                return Error(request.Id, -32602, "messageId is required");

            BigInteger sourceChainIdBig, messageIdBig;
            try
            {
                sourceChainIdBig = sourceChainIdHex.HexToBigInteger(false);
                messageIdBig = messageIdHex.HexToBigInteger(false);
            }
            catch (Exception)
            {
                return Error(request.Id, -32602, "Invalid hex value for sourceChainId or messageId");
            }

            if (sourceChainIdBig < 0 || sourceChainIdBig > ulong.MaxValue)
                return Error(request.Id, -32602, "sourceChainId out of valid range");
            if (messageIdBig < 0 || messageIdBig > ulong.MaxValue)
                return Error(request.Id, -32602, "messageId out of valid range");

            var sourceChainId = (ulong)sourceChainIdBig;
            var messageId = (ulong)messageIdBig;

            var resultStore = context.GetService<IMessageResultStore>();
            if (resultStore == null)
                return Error(request.Id, -32603, "Message result store not available");

            var accumulator = context.GetService<IMessageMerkleAccumulator>();
            if (accumulator == null)
                return Error(request.Id, -32603, "Message accumulator not available");

            var result = await resultStore.GetByMessageIdAsync(sourceChainId, messageId);
            if (result == null)
                return Error(request.Id, -32602, $"Message {messageId} from chain {sourceChainId} not found");

            var leafCount = accumulator.GetLeafCount(sourceChainId);
            if (result.LeafIndex >= leafCount)
                return Error(request.Id, -32603, "Leaf index out of range — accumulator may not be rebuilt");

            try
            {
                var proof = accumulator.GenerateProof(sourceChainId, result.LeafIndex);
                var root = accumulator.GetRoot(sourceChainId);
                var lastProcessedId = accumulator.GetLastProcessedMessageId(sourceChainId);

                var proofHexList = new List<string>();
                foreach (var node in proof.ProofNodes)
                {
                    proofHexList.Add(node.ToHex(true));
                }

                var response = new Dictionary<string, object>
                {
                    ["sourceChainId"] = ToHex(sourceChainId),
                    ["messageId"] = ToHex(messageId),
                    ["leafIndex"] = ToHex(result.LeafIndex),
                    ["txHash"] = result.TxHash.ToHex(true),
                    ["success"] = result.Success,
                    ["dataHash"] = result.DataHash.ToHex(true),
                    ["merkleRoot"] = root.ToHex(true),
                    ["processedUpToMessageId"] = ToHex(lastProcessedId),
                    ["proof"] = proofHexList
                };

                return Success(request.Id, response);
            }
            catch (Exception)
            {
                return Error(request.Id, -32603, "Failed to generate proof; accumulator may be rebuilding");
            }
        }
    }
}
