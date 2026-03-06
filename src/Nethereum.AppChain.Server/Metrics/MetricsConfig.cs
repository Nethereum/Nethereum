namespace Nethereum.AppChain.Server.Metrics
{
    public class MetricsConfig
    {
        public bool Enabled { get; set; } = true;
        public int CollectionIntervalMs { get; set; } = 5000;
    }
}
