using System.Numerics;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public enum PaymasterType
    {
        None,
        Sponsored,
        Verifying
    }

    public class AAConfig
    {
        public bool Enabled { get; set; } = true;
        public PaymasterType Paymaster { get; set; } = PaymasterType.None;
        public BigInteger? PaymasterDeposit { get; set; }
        public bool AutoDeployFactory { get; set; } = true;

        public static AAConfig Default() => new()
        {
            Enabled = true,
            Paymaster = PaymasterType.None,
            AutoDeployFactory = true
        };

        public static AAConfig WithSponsorship(BigInteger? deposit = null) => new()
        {
            Enabled = true,
            Paymaster = PaymasterType.Sponsored,
            PaymasterDeposit = deposit,
            AutoDeployFactory = true
        };

        public static AAConfig WithVerifyingPaymaster(BigInteger? deposit = null) => new()
        {
            Enabled = true,
            Paymaster = PaymasterType.Verifying,
            PaymasterDeposit = deposit,
            AutoDeployFactory = true
        };
    }
}
