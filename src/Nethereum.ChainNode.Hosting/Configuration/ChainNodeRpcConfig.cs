using Nethereum.CoreChain.Rpc;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeRpcConfig
    {
        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 8545;

        public int MetricsPort { get; set; }

        public int MaxLogBlockRange { get; set; } = 10_000;

        public int MaxLogResults { get; set; } = 10_000;

        public long GasCap { get; set; } = 50_000_000;

        public string Modules { get; set; } = RpcModuleSet.DefaultModules;

        public string AllowedHosts { get; set; } = string.Empty;

        public string AllowedOrigins { get; set; } = string.Empty;

        public string Url => $"http://{Host}:{Port}";
    }
}
