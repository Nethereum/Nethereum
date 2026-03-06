namespace Nethereum.Consensus.Clique
{
    public class CliqueConfig
    {
        public int BlockPeriodSeconds { get; set; } = 15;
        public int EpochLength { get; set; } = 30000;
        public int WiggleTimeMs { get; set; } = 500;
        public List<string> InitialSigners { get; set; } = new();
        public string LocalSignerAddress { get; set; } = "";
        public string? LocalSignerPrivateKey { get; set; }
        public bool AllowEmptyBlocks { get; set; } = false;
        public int MaxSignersPerRound { get; set; } = 21;
        public int RecentSignersLimit { get; set; } = 0;
        public bool EnableVoting { get; set; } = true;

        public static CliqueConfig Default => new()
        {
            BlockPeriodSeconds = 15,
            EpochLength = 30000,
            WiggleTimeMs = 500,
            AllowEmptyBlocks = false,
            MaxSignersPerRound = 21,
            EnableVoting = true
        };

        public static CliqueConfig Fast => new()
        {
            BlockPeriodSeconds = 1,
            EpochLength = 30000,
            WiggleTimeMs = 100,
            AllowEmptyBlocks = false,
            MaxSignersPerRound = 21,
            EnableVoting = true
        };

        public static CliqueConfig DevMode => new()
        {
            BlockPeriodSeconds = 0,
            EpochLength = 30000,
            WiggleTimeMs = 0,
            AllowEmptyBlocks = true,
            MaxSignersPerRound = 21,
            EnableVoting = false
        };

        public int CalculateRecentSignersLimit()
        {
            if (RecentSignersLimit > 0)
                return RecentSignersLimit;
            return (InitialSigners.Count / 2) + 1;
        }
    }
}
