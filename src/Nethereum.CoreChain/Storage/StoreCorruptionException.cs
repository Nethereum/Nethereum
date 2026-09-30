using System;

namespace Nethereum.CoreChain.Storage
{
    public sealed class StoreCorruptionException : InvalidOperationException
    {
        public string Store { get; }
        public string Key { get; }

        public StoreCorruptionException(string store, string key, Exception inner)
            : base($"corruption decoding row in store '{store}' key '{key}': {inner.Message}", inner)
        {
            Store = store;
            Key = key;
        }
    }
}
