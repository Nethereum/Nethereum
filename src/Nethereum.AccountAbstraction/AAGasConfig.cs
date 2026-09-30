using System.Numerics;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "AAGasConfig - gas buffers, multipliers and receipt-poll settings")]
    public class AAGasConfig
    {
        public BigInteger? VerificationGasBuffer { get; set; }
        public BigInteger? CallGasBuffer { get; set; }
        public BigInteger? PreVerificationGasBuffer { get; set; }
        public decimal CallGasMultiplier { get; set; } = 1.0m;
        public decimal VerificationGasMultiplier { get; set; } = 1.0m;
        public int ReceiptPollIntervalMs { get; set; } = 1000;
        public int ReceiptTimeoutMs { get; set; } = 60000;

        public static AAGasConfig Default => new AAGasConfig
        {
            PreVerificationGasBuffer = 1000
        };
    }
}
