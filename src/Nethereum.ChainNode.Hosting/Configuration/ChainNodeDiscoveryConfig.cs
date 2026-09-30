namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeDiscoveryConfig
    {
        public bool DisableDiscv4 { get; set; }

        public int Discv4Port { get; set; }

        public bool DisableDiscv5 { get; set; }

        public int Discv5Port { get; set; }
    }
}
