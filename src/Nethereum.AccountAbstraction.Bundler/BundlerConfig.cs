using System.Numerics;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler
{
    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "BundlerConfig - every bundler knob and the three mode presets")]
    public class BundlerConfig
    {
        public string[] SupportedEntryPoints { get; set; } = Array.Empty<string>();

        public string BeneficiaryAddress { get; set; } = null!;

        public int MaxBundleSize { get; set; } = 10;

        public int MaxMempoolSize { get; set; } = 1000;

        public BigInteger MinPriorityFeePerGas { get; set; } = 0;

        public BigInteger MaxBundleGas { get; set; } = 15_000_000;

        public bool SkipUnderpricedOpsInAutoBundle { get; set; } = true;

        public int AutoBundleIntervalMs { get; set; } = 10_000;

        public int ReputationDecayIntervalMs { get; set; } = 3_600_000;

        public int BundleReceiptTimeoutSeconds { get; set; } = 90;

        public BigInteger ReceiptLogLookbackBlocks { get; set; } = 10_000;

        public bool StrictValidation { get; set; } = true;

        public bool SimulateValidation { get; set; } = true;

        public bool UnsafeMode { get; set; } = false;

        public bool EnableERC7562Validation { get; set; } = false;

        public BigInteger MinStake { get; set; } = 1_000_000_000_000_000_000;

        public uint MinUnstakeDelaySec { get; set; } = 86400;

        public int MaxUnstakedSenderMempoolCount { get; set; } = 4;

        public HashSet<string> WhitelistedAddresses { get; set; } = new();

        public HashSet<string> BlacklistedAddresses { get; set; } = new();

        public int MaxVerificationGas { get; set; } = 1_500_000;

        public BigInteger? ChainId { get; set; }

        public string Hardfork { get; set; }

        public Nethereum.EVM.ChainForkSchedule ForkSchedule { get; set; }

        public Nethereum.EVM.ChainForkSchedule ResolveForkSchedule(long chainId) =>
            ForkSchedule
            ?? (string.IsNullOrEmpty(Hardfork)
                ? null
                : Nethereum.EVM.ChainForkSchedule.Running(
                    chainId, Nethereum.EVM.HardforkNames.Parse(Hardfork)));

        public bool EnableBlsAggregation { get; set; } = false;

        public string[] BlsAggregatorAddresses { get; set; } = Array.Empty<string>();

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

        public static BundlerConfig CreateStandardConfig(string entryPoint, string beneficiary) => new()
        {
            SupportedEntryPoints = new[] { entryPoint },
            BeneficiaryAddress = beneficiary,
            StrictValidation = true,
            SimulateValidation = true,
            EnableERC7562Validation = true,
            MinPriorityFeePerGas = 1_000_000_000,
            AutoBundleIntervalMs = 10_000,
            UnsafeMode = false
        };

        public static BundlerConfig CreateProductionConfig(string entryPoint, string beneficiary) => new()
        {
            SupportedEntryPoints = new[] { entryPoint },
            BeneficiaryAddress = beneficiary,
            StrictValidation = true,
            SimulateValidation = true,
            EnableERC7562Validation = true,
            MinPriorityFeePerGas = 1_000_000_000,
            AutoBundleIntervalMs = 10_000,
            UnsafeMode = false,
            MinStake = 1_000_000_000_000_000_000,
            MinUnstakeDelaySec = 86400
        };
    }
}
