namespace Nethereum.DevChain.Hosting
{
    public class EngineApiServerConfig
    {
        public const string DefaultJwtSecretFileName = "jwt.hex";
        public const int DefaultPort = 8551;

        public string JwtSecretPath { get; set; } = "./" + DefaultJwtSecretFileName;
        public string BindAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = DefaultPort;
    }
}
