using System.Collections.Generic;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public class MultiChainHubConfig
    {
        public bool Enabled { get; set; }
        public ulong AppChainId { get; set; }
        public string SequencerPrivateKey { get; set; } = "";

        public int AnchorCadence { get; set; } = 100;
        public int AnchorIntervalMs { get; set; } = 60000;

        public bool AcknowledgmentEnabled { get; set; } = true;
        public int AcknowledgmentIntervalMs { get; set; } = 30000;

        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;
        public int MaxMessagesPerBlock { get; set; } = 50;

        public List<HubChainEndpoint> HubEndpoints { get; set; } = new();
    }

    public class HubChainEndpoint
    {
        public ulong ChainId { get; set; }
        public string RpcUrl { get; set; } = "";
        public string HubContractAddress { get; set; } = "";
    }
}
