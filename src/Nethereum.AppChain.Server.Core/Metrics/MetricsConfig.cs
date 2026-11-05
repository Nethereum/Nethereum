namespace Nethereum.AppChain.Server.Metrics
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "MetricsConfig")]
    public class MetricsConfig
    {
        public bool Enabled { get; set; } = true;
        public int CollectionIntervalMs { get; set; } = 5000;
    }
}
