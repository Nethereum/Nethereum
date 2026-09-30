using System;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class JournalingPathNodeStore : ITrieNodeStore, IContractStorageWipeable, IRawNodeReader, ICacheInvalidatable
    {
        private readonly RocksDbPathTrieNodeStore _inner;
        private readonly RocksDbNodeReverseDiffStore _journal;
        private readonly INodeCommitBlockSource _blockSource;
        private readonly INodeHistoryFloorPolicy _floor;
        private readonly int _pruneInterval;
        private long _commitsSincePrune;

        public JournalingPathNodeStore(
            RocksDbPathTrieNodeStore inner,
            RocksDbNodeReverseDiffStore journal,
            INodeCommitBlockSource blockSource,
            INodeHistoryFloorPolicy floor,
            int pruneInterval)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _blockSource = blockSource ?? throw new ArgumentNullException(nameof(blockSource));
            _floor = floor ?? throw new ArgumentNullException(nameof(floor));
            if (pruneInterval < 1) throw new ArgumentOutOfRangeException(nameof(pruneInterval), "prune interval must be >= 1");
            _pruneInterval = pruneInterval;
        }

        public void Commit(TrieNodeSet nodes)
        {
            var block = _blockSource.CurrentBlock;
            if (!block.HasValue)
            {
                _inner.Commit(nodes);
                return;
            }

            _journal.RecordAndCommit(block.Value, _inner, nodes);

            if (Interlocked.Increment(ref _commitsSincePrune) >= _pruneInterval)
            {
                Interlocked.Exchange(ref _commitsSincePrune, 0);
                _journal.PruneBelow(_floor.FloorFor(block.Value));
            }
        }

        public byte[] Get(Node reference) => _inner.Get(reference);
        public bool Contains(Node reference) => _inner.Contains(reference);
        public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        public void Flush() => _inner.Flush();
        public void Clear() => _inner.Clear();

        public byte[] TryGetRawNode(byte[] owner, byte[] path) => _inner.TryGetRawNode(owner, path);

        public void ClearCache() => _inner.ClearCache();

        public void DeleteRange(byte[] owner)
        {
            var block = _blockSource.CurrentBlock;
            if (block.HasValue)
                _journal.RecordAndDeleteRange(block.Value, _inner, owner);
            else
                _inner.DeleteRange(owner);
        }
    }
}
