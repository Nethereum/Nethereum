namespace Nethereum.CoreChain.Prefetch
{
    public readonly record struct PrefetchOutcome(int Prefetched, int Skipped, int Failed);
}
