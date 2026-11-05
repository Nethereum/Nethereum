namespace Nethereum.CoreChain.Prefetch
{
    public readonly record struct TriePathOutcome(int Enqueued, int Warmed, int Dropped, int Failed, int Skipped);
}
