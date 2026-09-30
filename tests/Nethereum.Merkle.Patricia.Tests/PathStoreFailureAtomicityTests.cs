using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class PathStoreFailureAtomicityTests
    {
        private static readonly Sha3KeccackHashProvider Hp = new();
        private static readonly Nethereum.Util.Sha3Keccack Keccak = new();

        private sealed class FlakyPathStore : ITrieNodeStore
        {
            private readonly InMemoryPathNodeStore _inner = new();
            private readonly int _throwOnCommit;
            private int _commits;
            public FlakyPathStore(int throwOnCommit) { _throwOnCommit = throwOnCommit; }

            public void Commit(TrieNodeSet nodes)
            {
                if (++_commits == _throwOnCommit)
                    throw new InvalidOperationException("injected commit failure");
                _inner.Commit(nodes);
            }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private static byte[] K(int i) => Keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8), 0xAB });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 1) });

        [Fact]
        public void FailedCommit_LeavesPersistFlagsSet_SoRetryReWritesEverything()
        {
            var store = new FlakyPathStore(throwOnCommit: 1);
            var trie = new PatriciaTrie(store, Hp);

            for (int i = 0; i < 200; i++) trie.Put(K(i), V(i));

            Assert.Throws<InvalidOperationException>(() => trie.SaveDirtyNodesToStorage());

            trie.SaveDirtyNodesToStorage();
            var rootHash = trie.Root.GetHash();

            var reloaded = PatriciaTrie.LoadFromStorage(rootHash, store);
            for (int i = 0; i < 200; i++)
                Assert.Equal(V(i), reloaded.Get(K(i)));
        }

        [Fact]
        public void ColdLoadedNode_IsBornPersisted_NotReWrittenAndTombstonesOnDelete()
        {
            var store = new InMemoryPathNodeStore();
            var trie = new PatriciaTrie(store, Hp);
            for (int i = 0; i < 200; i++) trie.Put(K(i), V(i));
            trie.SaveDirtyNodesToStorage();
            var rootHash = trie.Root.GetHash();
            var countAfterFirstSave = store.Count;

            var reopened = PatriciaTrie.LoadFromStorage(rootHash, store);
            reopened.Delete(K(0));
            reopened.SaveDirtyNodesToStorage();

            Assert.True(store.Count <= countAfterFirstSave,
                $"cold-loaded nodes were re-written as dirty (before={countAfterFirstSave}, after={store.Count})");

            var final = PatriciaTrie.LoadFromStorage(reopened.Root.GetHash(), store);
            Assert.Null(final.Get(K(0)));
            for (int i = 1; i < 200; i++)
                Assert.Equal(V(i), final.Get(K(i)));
        }
    }
}
