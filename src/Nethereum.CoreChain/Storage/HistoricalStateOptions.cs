using System.Numerics;

namespace Nethereum.CoreChain.Storage
{
    public class HistoricalStateOptions
    {
        public BigInteger MaxHistoryBlocks { get; set; } = 256;
        public bool EnablePruning { get; set; } = true;
        public int PruningIntervalBlocks { get; set; } = 64;

        public static HistoricalStateOptions Default => new()
        {
            MaxHistoryBlocks = 256,
            EnablePruning = true,
            PruningIntervalBlocks = 64
        };

        public static HistoricalStateOptions FullArchive => new()
        {
            MaxHistoryBlocks = 0,
            EnablePruning = false,
            PruningIntervalBlocks = 0
        };

        public static HistoricalStateOptions DevChainDefault => new()
        {
            MaxHistoryBlocks = 128,
            EnablePruning = true,
            PruningIntervalBlocks = 32
        };
    }
}
