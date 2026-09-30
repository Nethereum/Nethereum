namespace Nethereum.Node.HarnessServer
{
    public class HarnessServerOptions
    {
        public string DataDir { get; set; } = "/data";
        public string HttpAddr { get; set; } = "127.0.0.1";
        public int HttpPort { get; set; } = 8545;
        public string AuthRpcAddr { get; set; } = "127.0.0.1";
        public int AuthRpcPort { get; set; } = 8551;
        public string? JwtSecretPath { get; set; }
        public string GenesisPath { get; set; } = "/network-config/genesis.json";
        public string ImportChainRlpPath { get; set; } = "/chain.rlp";
        public string ImportBlocksDir { get; set; } = "/blocks";
        public bool Verbose { get; set; }
    }
}
