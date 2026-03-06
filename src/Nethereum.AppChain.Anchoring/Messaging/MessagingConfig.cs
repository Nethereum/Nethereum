using System.Collections.Generic;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessagingConfig
    {
        public bool Enabled { get; set; }
        public int PollIntervalMs { get; set; } = 5000;
        public int MaxMessagesPerPoll { get; set; } = 100;
        public List<SourceChainConfig> SourceChains { get; set; } = new();
        public long StartAtBlockNumber { get; set; } = 0;
        public uint MinimumBlockConfirmations { get; set; } = 12;
        public int ReorgBuffer { get; set; } = 0;
        public int BlocksPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
    }

    public class SourceChainConfig
    {
        public ulong ChainId { get; set; }
        public string RpcUrl { get; set; } = "";
        public string HubContractAddress { get; set; } = "";
    }
}
