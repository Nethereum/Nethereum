namespace Nethereum.AppChain.Server.Configuration
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainAnchoringConfig")]
    public sealed class AppChainAnchoringConfig
    {
        public string? L1RpcUrl { get; set; }

        public string? ContractAddress { get; set; }

    }
}
