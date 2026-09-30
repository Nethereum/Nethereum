namespace Nethereum.AppChain.Server.Configuration
{
    public sealed class AppChainGenesisConfig
    {
        public SignerKeyPair Owner { get; set; } = new SignerKeyPair();

        public bool DeployCreate2Factory { get; set; } = true;
    }
}
