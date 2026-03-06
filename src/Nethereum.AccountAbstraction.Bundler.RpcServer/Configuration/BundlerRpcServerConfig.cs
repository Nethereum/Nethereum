using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration
{
    public class BundlerRpcServerConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 4337;
        public string RpcUrl { get; set; } = "http://localhost:8545";
        public BigInteger ChainId { get; set; } = 1;
        public string[] SupportedEntryPoints { get; set; } = Array.Empty<string>();
        public string BeneficiaryAddress { get; set; } = null!;
        public string? PrivateKey { get; set; }
        public int MaxBundleSize { get; set; } = 10;
        public int MaxMempoolSize { get; set; } = 1000;
        public BigInteger MinPriorityFeePerGas { get; set; } = 0;
        public BigInteger MaxBundleGas { get; set; } = 15_000_000;
        public int AutoBundleIntervalMs { get; set; } = 10_000;
        public bool StrictValidation { get; set; } = true;
        public bool SimulateValidation { get; set; } = true;
        public bool UnsafeMode { get; set; } = false;
        public bool Verbose { get; set; } = false;
        public bool EnableDebugMethods { get; set; } = false;

        public BundlerConfig ToBundlerConfig()
        {
            return new BundlerConfig
            {
                SupportedEntryPoints = SupportedEntryPoints,
                BeneficiaryAddress = BeneficiaryAddress,
                MaxBundleSize = MaxBundleSize,
                MaxMempoolSize = MaxMempoolSize,
                MinPriorityFeePerGas = MinPriorityFeePerGas,
                MaxBundleGas = MaxBundleGas,
                AutoBundleIntervalMs = AutoBundleIntervalMs,
                StrictValidation = StrictValidation,
                SimulateValidation = SimulateValidation,
                UnsafeMode = UnsafeMode,
                ChainId = ChainId
            };
        }

        public static BundlerRpcServerConfig CreateDefault(
            string entryPoint,
            string beneficiary,
            string rpcUrl,
            BigInteger chainId)
        {
            return new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { entryPoint },
                BeneficiaryAddress = beneficiary,
                RpcUrl = rpcUrl,
                ChainId = chainId
            };
        }

        public static BundlerRpcServerConfig CreateAppChainConfig(
            string entryPoint,
            string beneficiary,
            string rpcUrl,
            BigInteger chainId)
        {
            return new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { entryPoint },
                BeneficiaryAddress = beneficiary,
                RpcUrl = rpcUrl,
                ChainId = chainId,
                StrictValidation = false,
                AutoBundleIntervalMs = 1000,
                MinPriorityFeePerGas = 0,
                UnsafeMode = true
            };
        }
    }
}
