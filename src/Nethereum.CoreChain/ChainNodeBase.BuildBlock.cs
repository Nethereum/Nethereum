using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<BlockProductionResult> BuildBlockOnHeadAsync(
            byte[] parentHash,
            IReadOnlyList<ISignedTransaction> transactions,
            bool orderLikeMempool,
            BlockProductionOptions options)
        {
            var parentHeader = await _blockStore.GetLatestAsync().ConfigureAwait(false);
            var headHash = parentHeader != null
                ? await _blockStore.GetHashByNumberAsync(parentHeader.BlockNumber).ConfigureAwait(false)
                : null;
            if (headHash == null || parentHash == null || !ByteUtil.AreEqual(headHash, parentHash))
                throw new Rpc.RpcException(-32000, "parentHash is not current head");

            var isolatedState = await BuildIsolatedSimulateStateStoreAsync(null).ConfigureAwait(false);
            var producer = CreateIsolatedBlockProducer(isolatedState, Config.ResolveActivations());

            var built = await producer
                .BuildBlockOnParentAsync(transactions, orderLikeMempool, options, parentHash, parentHeader)
                .ConfigureAwait(false);

            if (!orderLikeMempool)
            {
                var included = new HashSet<string>(built.IncludedTransactions.Select(t => t.Hash.ToHex(true)));
                var rejected = transactions.FirstOrDefault(t => !included.Contains(t.Hash.ToHex(true)));
                if (rejected != null)
                    throw new Rpc.RpcException(-32000,
                        $"transaction {rejected.Hash.ToHex(true)} cannot be applied on block {parentHeader.BlockNumber}");
            }

            return built;
        }
    }
}
