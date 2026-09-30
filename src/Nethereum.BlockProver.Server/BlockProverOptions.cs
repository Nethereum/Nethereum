namespace Nethereum.BlockProver.Server
{
    public class BlockProverOptions
    {
        public bool Enabled { get; set; } = false;

        public int PollIntervalMs { get; set; } = 1000;

        public int MaxRetries { get; set; } = 3;

        public int RetryDelayMs { get; set; } = 1000;
    }
}
