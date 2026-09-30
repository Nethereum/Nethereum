using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    internal sealed class OrderingBlockSource : IBlockSource
    {
        private readonly IChainStoreBundle _bundle;
        private readonly ulong _from;
        private readonly ulong _to;
        private readonly byte[] _anchorHash;

        public OrderingBlockSource(IChainStoreBundle bundle, ulong fromInclusive, ulong toInclusive, byte[] anchorHash = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _from = fromInclusive;
            _to = toInclusive;
            _anchorHash = anchorHash;
        }

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            ulong start = fromBlock > _from ? fromBlock : _from;
            byte[] prevHash = start == _from
                ? _anchorHash
                : (start > 0 ? await _bundle.Blocks.GetHashByNumberAsync(start - 1).ConfigureAwait(false) : null);
            for (ulong n = start; n <= _to; n++)
            {
                ct.ThrowIfCancellationRequested();
                var header = await _bundle.Blocks.GetByNumberAsync(n).ConfigureAwait(false);
                if (header == null) yield break;
                var hash = await _bundle.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                if (hash == null) yield break;
                if (prevHash != null && header.ParentHash != null
                    && !ByteUtil.AreEqual(header.ParentHash, prevHash))
                {
                    yield break;
                }
                var uncles = await _bundle.Uncles.GetByBlockHashAsync(hash).ConfigureAwait(false);
                var txs = await _bundle.Transactions.GetByBlockHashAsync(hash).ConfigureAwait(false);
                var withdrawals = await _bundle.Withdrawals.GetByBlockHashAsync(hash).ConfigureAwait(false);
                yield return new BlockBundle(
                    Header: header,
                    Transactions: txs ?? new List<ISignedTransaction>(),
                    Uncles: uncles ?? new List<BlockHeader>(),
                    Withdrawals: withdrawals,
                    HeaderHash: hash);
                prevHash = hash;
            }
        }

        public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
            => Task.FromResult(BlockSourceHealth.Healthy);

        public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
            => Task.CompletedTask;

        public DivergenceSignal LastChainBreak => null;
    }
}
