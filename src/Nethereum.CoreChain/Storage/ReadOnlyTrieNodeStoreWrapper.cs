using System;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// <see cref="ITrieNodeStore"/> facade that absorbs every
    /// <see cref="Merkle.Patricia.ITrieStorage.Put"/> in memory and falls
    /// back to the wrapped store for reads. Used by
    /// <see cref="BlockExecutor.ExecuteAsync"/> when
    /// <see cref="BlockExecutionOptions.ReadOnly"/> is set and the engine
    /// still wants the calculator's lazy-load semantics to work for
    /// reads. Also surfaces as a safety net: the engine skips the
    /// post-state root compute under ReadOnly, but any helper that
    /// happens to write a trie node (current or future) is silently
    /// no-opped.
    /// </summary>
    public sealed class ReadOnlyTrieNodeStoreWrapper : ITrieNodeStore
    {
        private readonly ITrieNodeStore _inner;

        public ReadOnlyTrieNodeStoreWrapper(ITrieNodeStore inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public byte[] Get(byte[] key) => _inner.Get(key);
        public bool ContainsKey(byte[] key) => _inner.ContainsKey(key);

        public void Put(byte[] key, byte[] value) { }
        public void Delete(byte[] key) { }
        public void Flush() { }
        public void Clear() { }
    }
}
