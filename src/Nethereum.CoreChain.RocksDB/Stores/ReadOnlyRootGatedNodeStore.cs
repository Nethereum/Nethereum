using System;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public abstract class ReadOnlyRootGatedNodeStore : ITrieNodeStore
    {
        protected static readonly byte[] EmptyOwner = new byte[0];
        protected static readonly byte[] RootPath = new byte[0];
        private static readonly IHashProvider HashProvider = Sha3KeccackHashProvider.Instance;

        public abstract byte[] Get(Node reference);

        public abstract bool Contains(Node reference);

        protected abstract byte[] ReadRootBlob();

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            var blob = ReadRootBlob();
            if (blob == null || blob.Length == 0) return false;
            return ByteUtil.AreEqual(HashProvider.ComputeHash(blob), stateRoot);
        }

        public byte[] Get(byte[] key) => throw new NotSupportedException("Read-only serving overlay; hash-keyed Get is not supported (walk via Get(Node)).");
        public void Put(byte[] key, byte[] value) => throw new NotSupportedException("Read-only serving overlay.");
        public void Delete(byte[] key) => throw new NotSupportedException("Read-only serving overlay.");
        public void Commit(TrieNodeSet nodes) => throw new NotSupportedException("Read-only serving overlay.");
        public void Clear() => throw new NotSupportedException("Read-only serving overlay.");
        public void Flush() { }
    }
}
