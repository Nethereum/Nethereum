using System;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public abstract class ReadOnlyRootGatedNodeStore : ITrieNodeStore
    {
        protected static readonly byte[] EmptyOwner = new byte[0];
        protected static readonly byte[] RootPath = new byte[0];

        public abstract byte[] Get(Node reference);

        public abstract bool Contains(Node reference);

        protected abstract byte[] ReadRootBlob();

        public bool ContainsKey(byte[] stateRoot) => TrieNodeVerification.RootBlobHashesTo(ReadRootBlob(), stateRoot);

        public byte[] Get(byte[] key) => throw new NotSupportedException("Read-only serving overlay; hash-keyed Get is not supported (walk via Get(Node)).");
        public void Put(byte[] key, byte[] value) => throw new NotSupportedException("Read-only serving overlay.");
        public void Delete(byte[] key) => throw new NotSupportedException("Read-only serving overlay.");
        public void Commit(TrieNodeSet nodes) => throw new NotSupportedException("Read-only serving overlay.");
        public void Clear() => throw new NotSupportedException("Read-only serving overlay.");
        public void Flush() { }
    }
}
