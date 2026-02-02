using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler
{
    /// <summary>
    /// Configuration for the bundler service.
    /// </summary>
    public class BundlerConfig
    {
        /// <summary>
        /// The EntryPoint contract addresses supported by this bundler.
        /// </summary>
        public string[] SupportedEntryPoints { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The beneficiary address that receives bundler fees.
        /// </summary>
        public string BeneficiaryAddress { get; set; } = null!;

        /// <summary>
        /// Maximum number of UserOperations per bundle.
        /// </summary>
        public int MaxBundleSize { get; set; } = 10;

        /// <summary>
        /// Maximum number of pending UserOperations in mempool.
        /// </summary>
        public int MaxMempoolSize { get; set; } = 1000;

        /// <summary>
        /// Minimum priority fee per gas required for UserOperations.
        /// </summary>
        public BigInteger MinPriorityFeePerGas { get; set; } = 0;

        /// <summary>
        /// Maximum gas limit for a single bundle transaction.
        /// </summary>
        public BigInteger MaxBundleGas { get; set; } = 15_000_000;

        /// <summary>
        /// Interval between automatic bundle submissions (milliseconds).
        /// Set to 0 to disable automatic bundling.
        /// </summary>
        public int AutoBundleIntervalMs { get; set; } = 10_000;

        /// <summary>
        /// Whether to enable strict ERC-4337 validation rules.
        /// Disable for simplified AppChain mode.
        /// </summary>
        public bool StrictValidation { get; set; } = true;

        /// <summary>
        /// Whether to simulate UserOperations before adding to mempool.
        /// </summary>
        public bool SimulateValidation { get; set; } = true;

        /// <summary>
        /// Gas overhead to add to verification gas estimates.
        /// </summary>
        public BigInteger VerificationGasOverhead { get; set; } = 10_000;

        /// <summary>
        /// Gas overhead to add to call gas estimates.
        /// </summary>
        public BigInteger CallGasOverhead { get; set; } = 21_000;

        /// <summary>
        /// Whether to use unsafe mode (skip some validations).
        /// Only for testing/development.
        /// </summary>
        public bool UnsafeMode { get; set; } = false;

        /// <summary>
        /// Whether to enable ERC-7562 trace-based opcode validation.
        /// When enabled, UserOperations are validated against forbidden opcodes
        /// and storage access rules using EVM simulation.
        /// </summary>
        public bool EnableERC7562Validation { get; set; } = false;

        /// <summary>
        /// Minimum stake required for entities to use staked-only opcodes (e.g., BALANCE, SELFBALANCE).
        /// Applies to senders, factories, paymasters, and aggregators.
        /// </summary>
        public BigInteger MinStake { get; set; } = 1_000_000_000_000_000_000; // 1 ETH

        /// <summary>
        /// Minimum unstake delay in seconds for staked entities.
        /// </summary>
        public uint MinUnstakeDelaySec { get; set; } = 86400; // 24 hours

        /// <summary>
        /// Whitelist of addresses that bypass reputation checks.
        /// </summary>
        public HashSet<string> WhitelistedAddresses { get; set; } = new();

        /// <summary>
        /// Blacklist of addresses that are always rejected.
        /// </summary>
        public HashSet<string> BlacklistedAddresses { get; set; } = new();

        /// <summary>
        /// Maximum number of verification gas steps (for simulation).
        /// </summary>
        public int MaxVerificationGas { get; set; } = 1_500_000;

        /// <summary>
        /// Chain ID (set automatically from web3 if not specified).
        /// </summary>
        public BigInteger? ChainId { get; set; }

        /// <summary>
        /// Whether to enable BLS signature aggregation for UserOperations.
        /// When enabled, operations with BLS signatures can be aggregated to reduce gas costs.
        /// </summary>
        public bool EnableBlsAggregation { get; set; } = false;

        /// <summary>
        /// Addresses of supported BLS aggregator contracts.
        /// Operations using these aggregators can have their signatures aggregated.
        /// </summary>
        public string[] BlsAggregatorAddresses { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Minimum number of operations required to perform aggregation.
        /// Below this threshold, operations are submitted individually.
        /// </summary>
        public int MinAggregationSize { get; set; } = 2;

        /// <summary>
        /// Creates a default configuration for AppChain (simplified mode).
        /// </summary>
        public static BundlerConfig CreateAppChainConfig(string entryPoint, string beneficiary) => new()
        {
            SupportedEntryPoints = new[] { entryPoint },
            BeneficiaryAddress = beneficiary,
            StrictValidation = false,
            SimulateValidation = true,
            MinPriorityFeePerGas = 0,
            AutoBundleIntervalMs = 1000,
            UnsafeMode = false
        };

        /// <summary>
        /// Creates a default configuration for standard ERC-4337 mode.
        /// </summary>
        public static BundlerConfig CreateStandardConfig(string entryPoint, string beneficiary) => new()
        {
            SupportedEntryPoints = new[] { entryPoint },
            BeneficiaryAddress = beneficiary,
            StrictValidation = true,
            SimulateValidation = true,
            EnableERC7562Validation = true,
            MinPriorityFeePerGas = 1_000_000_000, // 1 Gwei
            AutoBundleIntervalMs = 10_000,
            UnsafeMode = false
        };

        /// <summary>
        /// Creates a configuration for production bundler with full ERC-7562 validation.
        /// </summary>
        public static BundlerConfig CreateProductionConfig(string entryPoint, string beneficiary) => new()
        {
            SupportedEntryPoints = new[] { entryPoint },
            BeneficiaryAddress = beneficiary,
            StrictValidation = true,
            SimulateValidation = true,
            EnableERC7562Validation = true,
            MinPriorityFeePerGas = 1_000_000_000, // 1 Gwei
            AutoBundleIntervalMs = 10_000,
            UnsafeMode = false,
            MinStake = 1_000_000_000_000_000_000, // 1 ETH
            MinUnstakeDelaySec = 86400 // 24 hours
        };
    }
}
