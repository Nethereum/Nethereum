using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.MainnetChain.Server.Rpc
{
    /// <summary>
    /// Resolves <c>"finalized"</c> and <c>"safe"</c> labels against the
    /// <see cref="IFinalityCursorProvider"/> before falling through to the standard
    /// follower behaviour. With the light client active the labels track the trusted
    /// beacon finalized/optimistic execution-payload block numbers; without one the
    /// labels degrade to "latest" so callers that always ask for "finalized" keep
    /// working against a follower that has no consensus oracle.
    /// </summary>
    public sealed class MainnetChainEthGetBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

            var blockNumber = await ResolveLabelAsync(blockTag, context);

            var blockHeader = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);

            var txStore = context.GetService<ITransactionStore>();
            if (includeTransactions)
            {
                var signedTxs = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
                var transactions = signedTxs?
                    .Select((tx, index) => SignedTransactionExtensions.ToRpcTransaction(tx, blockHash, blockNumber, index))
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactions(blockHash, transactions));
            }
            else
            {
                var hashes = txStore != null ? await txStore.GetHashesByBlockHashAsync(blockHash) : null;
                var txHashes = hashes?
                    .Select(h => h?.ToHex(true))
                    .Where(h => h != null)
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes));
            }
        }

        internal static async Task<BigInteger> ResolveLabelAsync(string blockTag, RpcContext context)
        {
            var cursor = context.GetService<IFinalityCursorProvider>();
            var latest = await context.Node.GetBlockNumberAsync();
            return ResolveLabel(blockTag, cursor, latest);
        }

        public static BigInteger ResolveLabel(string blockTag, IFinalityCursorProvider? cursor, BigInteger latest)
        {
            if (blockTag == BlockParameter.BlockParameterType.finalized.ToString())
            {
                return cursor?.GetFinalizedBlockNumber() is BigInteger fin ? fin : latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.safe.ToString())
            {
                return cursor?.GetSafeBlockNumber() is BigInteger safe ? safe : latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.latest.ToString() ||
                blockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                return latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return 0;
            }

            return blockTag.HexToBigInteger(false);
        }
    }
}
