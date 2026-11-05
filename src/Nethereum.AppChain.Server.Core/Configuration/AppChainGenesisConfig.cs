namespace Nethereum.AppChain.Server.Configuration
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainGenesisConfig")]
    public sealed class AppChainGenesisConfig
    {
        public SignerKeyPair Owner { get; set; } = new SignerKeyPair();

        public bool DeployCreate2Factory { get; set; } = true;
    }
}
