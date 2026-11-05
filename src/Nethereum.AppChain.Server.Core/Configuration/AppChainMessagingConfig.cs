using System;

namespace Nethereum.AppChain.Server.Configuration
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainMessagingConfig")]
    public sealed class AppChainMessagingConfig
    {
        public bool Enabled { get; set; }

        public string[] HubSourceChains { get; set; } = Array.Empty<string>();

        public int PollIntervalMs { get; set; } = 5000;

        public int MaxMessagesPerPoll { get; set; } = 100;

        public bool AcknowledgmentEnabled { get; set; }

        public int AcknowledgmentIntervalMs { get; set; } = 30000;
    }
}
