using System;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class PathKeyedProofNodeStore : ReadOnlyRootGatedNodeStore
    {
        private readonly RocksDbPathTrieNodeStore _latest;

        public PathKeyedProofNodeStore(RocksDbPathTrieNodeStore latest)
        {
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
        }

        public override byte[] Get(Node reference) => reference == null ? null : _latest.Get(reference);

        public override bool Contains(Node reference) => reference != null && _latest.Contains(reference);

        protected override byte[] ReadRootBlob() => _latest.TryGetRaw(EmptyOwner, RootPath);
    }
}
