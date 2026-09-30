using System.Numerics;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Paymasters
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "SponsorResult - sponsored plus the data to attach, or the refusal")]
    public class SponsorResult
    {
        public bool IsSponsored { get; set; }
        public byte[] PaymasterAndData { get; set; } = Array.Empty<byte>();
        public string? PaymasterAddress { get; set; }
        public ulong ValidAfter { get; set; }
        public ulong ValidUntil { get; set; }
        public BigInteger EstimatedGasCost { get; set; }
        public string? Error { get; set; }

        public static SponsorResult Success(byte[] paymasterAndData, string paymasterAddress, ulong validUntil = 0, ulong validAfter = 0)
        {
            return new SponsorResult
            {
                IsSponsored = true,
                PaymasterAndData = paymasterAndData,
                PaymasterAddress = paymasterAddress,
                ValidUntil = validUntil,
                ValidAfter = validAfter
            };
        }

        public static SponsorResult Failure(string error)
        {
            return new SponsorResult
            {
                IsSponsored = false,
                Error = error
            };
        }
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "SponsorContext - what a sponsorship decision may be conditioned on")]
    public class SponsorContext
    {
        public string? SenderAddress { get; set; }
        public BigInteger? MaxGasPrice { get; set; }
        public ulong? ValidUntil { get; set; }
        public ulong? ValidAfter { get; set; }
        public string? Metadata { get; set; }
    }
}
