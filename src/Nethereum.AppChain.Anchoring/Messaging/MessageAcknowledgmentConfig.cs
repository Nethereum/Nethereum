namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageAcknowledgmentConfig
    {
        public bool Enabled { get; set; }
        public int IntervalMs { get; set; } = 30000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
    }
}
