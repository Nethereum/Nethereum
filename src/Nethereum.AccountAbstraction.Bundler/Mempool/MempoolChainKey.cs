using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public readonly record struct ChainKey(string Sender, BigInteger NonceKey, string EntryPoint);

    public static class MempoolChainKey
    {
        public static ChainKey Of(MempoolEntry entry) =>
            new ChainKey(
                entry.UserOperation.Sender?.ToLowerInvariant() ?? string.Empty,
                entry.UserOperation.Nonce >> 64,
                entry.EntryPoint?.ToLowerInvariant() ?? string.Empty);
    }
}
