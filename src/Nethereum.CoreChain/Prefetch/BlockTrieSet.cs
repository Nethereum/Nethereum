using System;
using System.Collections.Concurrent;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;

namespace Nethereum.CoreChain.Prefetch
{
    public sealed class BlockTrieSet
    {
        private readonly Func<byte[], byte[], PatriciaTrie> _openStorageTrie;
        private readonly ConcurrentDictionary<StorageTrieKey, PatriciaTrie> _storageTries = new ConcurrentDictionary<StorageTrieKey, PatriciaTrie>();
        private PatriciaTrie? _accountTrie;

        internal BlockTrieSet(byte[] parentRoot, PatriciaTrie accountTrie, Func<byte[], byte[], PatriciaTrie> openStorageTrie)
        {
            ParentRoot = parentRoot ?? throw new ArgumentNullException(nameof(parentRoot));
            _accountTrie = accountTrie ?? throw new ArgumentNullException(nameof(accountTrie));
            _openStorageTrie = openStorageTrie ?? throw new ArgumentNullException(nameof(openStorageTrie));
        }

        internal byte[] ParentRoot { get; }

        internal PatriciaTrie? AccountTrie => _accountTrie;

        internal PatriciaTrie StorageTrie(byte[] hashedAddress, byte[] root)
            => _storageTries.GetOrAdd(new StorageTrieKey(hashedAddress, root), _ => _openStorageTrie(hashedAddress, root));

        public PatriciaTrie? TakeAccountTrie(byte[] expectedRoot)
        {
            if (!ByteUtil.AreEqual(ParentRoot, expectedRoot)) return null;
            return System.Threading.Interlocked.Exchange(ref _accountTrie, null);
        }

        internal PatriciaTrie? TakeStorageTrie(byte[] hashedAddress, byte[] root)
            => _storageTries.TryRemove(new StorageTrieKey(hashedAddress, root), out var trie) ? trie : null;

        private readonly struct StorageTrieKey : IEquatable<StorageTrieKey>
        {
            private readonly byte[] _hashedAddress;
            private readonly byte[] _root;

            public StorageTrieKey(byte[] hashedAddress, byte[] root)
            {
                _hashedAddress = hashedAddress;
                _root = root;
            }

            public bool Equals(StorageTrieKey other)
                => ByteUtil.AreEqual(_hashedAddress, other._hashedAddress) && ByteUtil.AreEqual(_root, other._root);

            public override bool Equals(object? obj) => obj is StorageTrieKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Mix(_hashedAddress) * 31) ^ Mix(_root);
                }
            }

            private static int Mix(byte[] bytes)
            {
                var hash = 17;
                for (var i = 0; i < bytes.Length; i++) hash = (hash * 31) + bytes[i];
                return hash;
            }
        }
    }
}
