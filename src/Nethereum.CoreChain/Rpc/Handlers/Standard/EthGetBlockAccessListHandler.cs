using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    /// <summary>
    /// Serves the retained EIP-7928 block access list, decoded into JSON, per execution-apis
    /// <c>eth_getBlockAccessList</c>.
    ///
    /// <para>The list is the one thing about a block a peer cannot rebuild from the block. EIP-7928:
    /// <i>"The <c>BlockAccessList</c> is not included in the block body. The EL stores BALs separately"</i>
    /// — so this method answers from the store, never by re-executing.</para>
    ///
    /// <para>Three negative answers, and the spec keeps them apart: a block nobody has is <c>null</c>,
    /// a block from before the field existed is <c>-32001</c>, and a block whose list the node no longer
    /// keeps is <c>4444</c>. Collapsing them would tell a syncing peer to give up where it should ask
    /// someone else.</para>
    ///
    /// <para>A node holding no access-list store at all is a fourth case the spec does not name, and it
    /// is none of those three — see <see cref="MethodNotSupported"/>.</para>
    /// </summary>
    public class EthGetBlockAccessListHandler : RpcHandlerBase
    {
        public override string MethodName => "eth_getBlockAccessList";

        private const int ResourceNotFound = -32001;

        private const int PrunedHistoryUnavailable = 4444;

        /// <summary>
        /// Ours: execution-apis names no answer for a node that retains no access lists at all, and
        /// <c>4444</c> is not it — pruning is a claim about one block's past, an absent store is a claim
        /// about this node's capability. EIP-1474 registers <c>-32004</c>: <i>"Method not supported |
        /// Method is not implemented"</i>. A caller reading <c>4444</c> asks another peer for THAT
        /// block; a caller reading this stops asking THIS node for any block's list.
        /// </summary>
        private const int MethodNotSupported = -32004;

        private const string NoRetentionMessage =
            "Method not supported: this node retains no block access lists";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockParameter = GetParam<string>(request, 0);

            if (IsPendingTag(blockParameter)) return Success(request.Id, null);

            var blockHash = await ResolveBlockHashAsync(blockParameter, context);
            if (blockHash == null) return Success(request.Id, null);

            var header = await context.Node.Blocks.GetByHashAsync(blockHash);
            if (header == null) return Success(request.Id, null);

            if (PredatesAmsterdam(header))
                return Error(request.Id, ResourceNotFound, "Resource not found");

            var retainedLists = context.Node.BlockAccessLists;
            if (retainedLists == null)
                return Error(request.Id, MethodNotSupported, NoRetentionMessage);

            var retained = await retainedLists.GetByBlockHashAsync(blockHash);
            if (retained == null)
                return Error(request.Id, PrunedHistoryUnavailable, "Pruned history unavailable");

            return Success(request.Id, DecodeToWireAccounts(retained));
        }

        private static bool IsPendingTag(string blockParameter) =>
            blockParameter == BlockParameter.BlockParameterType.pending.ToString();

        private static async Task<byte[]> ResolveBlockHashAsync(string blockParameter, RpcContext context)
        {
            if (IsBlockHash(blockParameter)) return blockParameter.HexToByteArray();

            var blockNumber = await ResolveBlockNumberAsync(blockParameter, context);
            return await context.Node.GetBlockHashByNumberAsync(blockNumber);
        }

        private const int BlockHashHexLength = 66;

        private static bool IsBlockHash(string blockParameter) =>
            blockParameter != null &&
            blockParameter.Length == BlockHashHexLength &&
            blockParameter.StartsWith("0x");

        /// <summary>
        /// EIP-7928: <i>"We introduce a new field to the block header, <c>block_access_list_hash</c>"</i> —
        /// a header carrying no such field is a header from before the field existed. Asked of the block
        /// rather than of the fork schedule, so a node with no schedule still answers correctly.
        /// </summary>
        private static bool PredatesAmsterdam(BlockHeader header) => header.BlockAccessListHash == null;

        private static object DecodeToWireAccounts(byte[] blockAccessListRlp) =>
            BlockAccessListRpcMapper.ToDto(BlockAccessListRLPEncoder.Current.Decode(blockAccessListRlp));
    }
}
