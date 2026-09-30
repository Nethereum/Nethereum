namespace Nethereum.AppChain.Server.Configuration
{
    public sealed class AppChainMudConfig
    {
        public bool DeployWorld { get; set; } = true;

        public byte[] WorldSalt { get; set; } = new byte[32];
    }
}
