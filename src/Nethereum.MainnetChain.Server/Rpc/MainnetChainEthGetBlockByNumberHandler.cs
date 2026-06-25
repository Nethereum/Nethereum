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

            // Use IChainNode.Transactions, not context.GetService<ITransactionStore>():
            // the bare ITransactionStore interface isn't registered in DI (only the
            // IChainStoreBundle whose Transactions property exposes it), so the DI
            // lookup returns null and we'd silently return an empty transactions
            // array. Every other handler that needs txs (count, receipts, fee
            // history) goes through context.Node.* — keep parity here.
            var txStore = context.Node.Transactions;
            // Load full signed txs even in hash-only mode so we can compute the canonical
            // block "size" (full RLP length, not just the header). The cost is one extra
            // store read per block; getting size right is a hard correctness requirement
            // for any explorer / indexer / parity check (otherwise our size is ~530 bytes
            // for every block regardless of body).
            var signedTxs = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
            var uncles = context.Node.Uncles != null
                ? await context.Node.Uncles.GetByBlockHashAsync(blockHash)
                : null;

            var fullBlockSize = Nethereum.CoreChain.Rpc.BlockHeaderExtensions.CalculateFullBlockSize(
                blockHeader, signedTxs, uncles);

            var uncleHashes = uncles?
                .Select(u => Nethereum.Util.Sha3Keccack.Current.CalculateHash(
                    Nethereum.Model.BlockHeaderEncoder.Current.Encode(u)).ToHex(true))
                .ToArray() ?? new string[0];

            if (includeTransactions)
            {
                var transactions = signedTxs?
                    .Select((tx, index) => SignedTransactionExtensions.ToRpcTransaction(tx, blockHash, blockNumber, index))
                    .ToArray();
                var block = blockHeader.ToBlockWithTransactions(blockHash, transactions, blockSize: fullBlockSize);
                block.Uncles = uncleHashes;
                return Success(request.Id, block);
            }
            else
            {
                var txHashes = signedTxs?
                    .Select(tx => tx.Hash?.ToHex(true))
                    .Where(h => h != null)
                    .ToArray() ?? new string[0];
                var block = blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes, blockSize: fullBlockSize);
                block.Uncles = uncleHashes;
                return Success(request.Id, block);
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
