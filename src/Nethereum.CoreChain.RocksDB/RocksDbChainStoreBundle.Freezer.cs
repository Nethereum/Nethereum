using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed partial class RocksDbChainStoreBundle
    {
        private Task AppendBlocksToFreezerThenRest(IReadOnlyList<PersistableBlock> blocks, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var handled = _freezerAppendService.AppendFreezeEligiblePrefix(
                blocks, _freezerAppendService.ResolveFreezeBoundary(), ct);
            if (handled == blocks.Count)
                return Task.CompletedTask;

            var suffix = handled == 0 ? blocks : blocks.Skip(handled).ToList();
            return PersistBlocksToRocksDb(suffix, ct);
        }

        public System.Numerics.BigInteger FreezeBoundary =>
            _freezerAppendService?.ResolveFreezeBoundary() ?? System.Numerics.BigInteger.MinusOne;

        public Task<int> PersistBackpressureExemptPrefixAsync(
            IReadOnlyList<PersistableBlock> blocks, System.Numerics.BigInteger freezeBoundary, CancellationToken ct = default)
        {
            if (_freezerAppendService == null || blocks == null || blocks.Count == 0)
                return Task.FromResult(0);

            return Task.FromResult(_freezerAppendService.AppendFreezeEligiblePrefix(blocks, freezeBoundary, ct));
        }

        public static ulong ReconcileBodyCursorToFreezerHead(ulong bodyCursor, long freezerItems, System.Numerics.BigInteger freezeBoundary)
        {
            var withinFrozenBand = freezeBoundary >= 0 && bodyCursor <= freezeBoundary;
            if (freezerItems <= 0)
                return withinFrozenBand ? 0UL : bodyCursor;

            var freezerHead = (ulong)(freezerItems - 1);
            if (freezerHead >= bodyCursor) return freezerHead;
            return withinFrozenBand ? freezerHead : bodyCursor;
        }
    }
}
