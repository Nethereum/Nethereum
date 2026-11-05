using System.Collections.Generic;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DataIndexing, "appchain-anchoring-postgres", "Anchor index options")]
    public sealed class AnchorIndexProcessingOptions
    {
        public string RpcUrl { get; set; } = "http://localhost:8545";
        public string AnchorContractAddress { get; set; } = "";
        public long StartAtBlockNumberIfNotProcessed { get; set; }
        public int NumberOfBlocksPerRequest { get; set; } = 1000;
        public int PollIntervalMs { get; set; } = 5000;
        public uint MinimumBlockConfirmations { get; set; }
        public int ReorgBuffer { get; set; }
        public List<long> ChainIdFilter { get; set; } = new();
    }
}
