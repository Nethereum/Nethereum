namespace Nethereum.AppChain.Server.Configuration
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainMudConfig")]
    public sealed class AppChainMudConfig
    {
        public bool DeployWorld { get; set; } = true;

        public byte[] WorldSalt { get; set; } = new byte[32];
    }
}
