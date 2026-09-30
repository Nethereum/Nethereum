using System;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class AsOfBlockNodeStore : ReadOnlyRootGatedNodeStore
    {
        private readonly RocksDbNodeReverseDiffStore _journal;
        private readonly RocksDbPathTrieNodeStore _latest;
        private readonly ulong _targetBlock;

        public AsOfBlockNodeStore(RocksDbNodeReverseDiffStore journal, RocksDbPathTrieNodeStore latest, ulong targetBlock)
        {
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
            _targetBlock = targetBlock;
        }

        public override byte[] Get(Node reference)
        {
            if (reference == null) return null;
            var blob = _journal.FindBlobAsOf(reference.Owner, reference.Path, _targetBlock);
            if (blob != null)
                return blob.Length >= 1 ? blob : null;
            return _latest.Get(reference);
        }

        public override bool Contains(Node reference)
        {
            if (reference == null) return false;
            var blob = _journal.FindBlobAsOf(reference.Owner, reference.Path, _targetBlock);
            if (blob != null) return blob.Length >= 1;
            return _latest.Contains(reference);
        }

        protected override byte[] ReadRootBlob()
            => _journal.FindBlobAsOf(EmptyOwner, RootPath, _targetBlock)
               ?? _latest.TryGetRaw(EmptyOwner, RootPath);
    }
}
