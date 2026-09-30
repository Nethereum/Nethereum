using System;
using Nethereum.CoreChain.Freezer;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB.Stores
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "FreezerReadRouter - which tier owns a block: the frozen archive or the recent band")]
    public sealed class FreezerReadRouter
    {
        private readonly Nethereum.Freezer.Freezer _freezer;
        private readonly IRandomKeyIndexStore _recentIndex;
        private readonly IRandomKeyIndexStore _frozenIndex;

        public FreezerReadRouter(Nethereum.Freezer.Freezer freezer, IRandomKeyIndexStore recentIndex, IRandomKeyIndexStore frozenIndex)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
            _recentIndex = recentIndex ?? throw new ArgumentNullException(nameof(recentIndex));
            _frozenIndex = frozenIndex ?? throw new ArgumentNullException(nameof(frozenIndex));
        }

        public long FrozenCount() => _freezer.Items;

        public bool TryResolveNumber(byte[] hash, out long number) =>
            _recentIndex.TryGetBlockNumberByHash(hash, out number) || _frozenIndex.TryGetBlockNumberByHash(hash, out number);

        public bool TryResolveTxLocation(byte[] txHash, out long blockNumber, out int txIndex) =>
            _recentIndex.TryGetTxLocation(txHash, out blockNumber, out txIndex) || _frozenIndex.TryGetTxLocation(txHash, out blockNumber, out txIndex);
    }
}
