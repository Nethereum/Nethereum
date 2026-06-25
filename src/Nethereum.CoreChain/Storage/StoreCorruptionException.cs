using System;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Thrown when a persistence-layer read finds data that exists but cannot
    /// be decoded into the expected shape. Distinct from "no data at this key"
    /// (which surfaces as <c>null</c>) — corruption means a downstream caller
    /// must NOT treat the missing decoded value as absent.
    /// </summary>
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
