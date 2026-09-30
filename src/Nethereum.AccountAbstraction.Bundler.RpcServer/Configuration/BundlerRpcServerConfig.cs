using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration
{
    public class BundlerRpcServerConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 4337;
        public string RpcUrl { get; set; } = "http://localhost:8545";
        public BigInteger ChainId { get; set; } = 0;
        public string[] SupportedEntryPoints { get; set; } = Array.Empty<string>();
        public string BeneficiaryAddress { get; set; } = null!;
        public string? PrivateKey { get; set; }
        public int MaxBundleSize { get; set; } = 10;
        public int MaxMempoolSize { get; set; } = 1000;
        public BigInteger MinPriorityFeePerGas { get; set; } = 0;
        public BigInteger MaxBundleGas { get; set; } = 15_000_000;
        public int AutoBundleIntervalMs { get; set; } = 10_000;

        public int ReputationDecayIntervalMs { get; set; } = 3_600_000;

        public bool StrictValidation { get; set; } = true;
        public bool SimulateValidation { get; set; } = true;
        public bool UnsafeMode { get; set; } = false;
        public bool Verbose { get; set; } = false;
        public bool EnableDebugMethods { get; set; } = false;

        /// <summary>
        /// Ceiling on a UserOperation's verificationGasLimit, enforced during structural
        /// validation (see UserOpValidator). This is a Nethereum resource-safety knob, not
        /// an ERC-4337/ERC-7562 spec requirement or an eth-infinitism reference-bundler
        /// setting - the reference bundler and spec impose no fixed ceiling here, relying
        /// instead on the block gas limit and the EntryPoint's own gas accounting.
        /// Default is raised well above the highest verificationGasLimit exercised by the
        /// eth-infinitism bundler-spec-tests compliance suite (5,000,000, used by
        /// EREP-020 test_staked_factory_on_account_failure for a legitimately expensive
        /// staked factory), so the server is conformant out of the box. Operators can
        /// still lower it via --maxVerificationGas for stricter DoS protection.
        /// </summary>
        public int MaxVerificationGas { get; set; } = 10_000_000;

        /// <summary>
        /// Whether to enable ERC-7562 trace-based opcode/storage validation (see
        /// Nethereum.AccountAbstraction.Bundler.Validation.ERC7562.ERC7562SimulationService).
        /// Defaults to false to preserve existing RpcServer host behaviour; UserOpValidator
        /// wires its own IStateReader and EVM hardfork config, so no extra DI is required
        /// beyond forwarding this flag.
        /// </summary>
        public bool EnableERC7562Validation { get; set; } = false;

        /// <summary>
        /// Minimum stake (in wei) for an entity to be treated as staked by StakingInfoService
        /// (OP-080/OP-031/STO-031-033 staked-only rules). ERC-7562's SREP-010 defines "staked"
        /// as stake >= MIN_STAKE_VALUE (spec: a per-chain amount, "roughly $1000 equivalent");
        /// this default (1 ETH) is BundlerConfig's own default carried through unchanged.
        /// Previously unreachable from the RpcServer host (same host-gap class as
        /// MaxVerificationGas/EnableERC7562Validation): ToBundlerConfig() never forwarded it, so
        /// operators had no way to lower it for a given deployment or compliance run.
        /// </summary>
        public BigInteger MinStake { get; set; } = 1_000_000_000_000_000_000;

        /// <summary>
        /// Minimum unstake delay in seconds for an entity to be treated as staked. ERC-7562's
        /// SREP-010 defines MIN_UNSTAKE_DELAY = 86400 (1 day); this default carries BundlerConfig's
        /// own default unchanged. Compliance suites (e.g. eth-infinitism's bundler-spec-tests
        /// staked_contract() helper) stake with a 2-second delay for test speed - mirroring how
        /// the reference bundler is itself run with minUnstakeDelay=0 for that suite - so
        /// operators need this knob to lower the threshold for a compliance run without
        /// changing the spec-correct 1-day production default.
        /// </summary>
        public uint MinUnstakeDelaySec { get; set; } = 86400;

        public bool SkipUnderpricedOpsInAutoBundle { get; set; } = true;

        public int BundleReceiptTimeoutSeconds { get; set; } = 90;

        public BigInteger ReceiptLogLookbackBlocks { get; set; } = 10_000;

        public int MaxUnstakedSenderMempoolCount { get; set; } = 4;

        public HashSet<string> WhitelistedAddresses { get; set; } = new();

        public HashSet<string> BlacklistedAddresses { get; set; } = new();

        public string Hardfork { get; set; }

        public bool RequireSigner { get; set; } = true;

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
                ReputationDecayIntervalMs = ReputationDecayIntervalMs,
                StrictValidation = StrictValidation,
                SimulateValidation = SimulateValidation,
                UnsafeMode = UnsafeMode,
                ChainId = ChainId,
                MaxVerificationGas = MaxVerificationGas,
                EnableERC7562Validation = EnableERC7562Validation,
                MinStake = MinStake,
                MinUnstakeDelaySec = MinUnstakeDelaySec,
                SkipUnderpricedOpsInAutoBundle = SkipUnderpricedOpsInAutoBundle,
                BundleReceiptTimeoutSeconds = BundleReceiptTimeoutSeconds,
                ReceiptLogLookbackBlocks = ReceiptLogLookbackBlocks,
                MaxUnstakedSenderMempoolCount = MaxUnstakedSenderMempoolCount,
                WhitelistedAddresses = new HashSet<string>(WhitelistedAddresses, StringComparer.OrdinalIgnoreCase),
                BlacklistedAddresses = new HashSet<string>(BlacklistedAddresses, StringComparer.OrdinalIgnoreCase),
                Hardfork = Hardfork
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
