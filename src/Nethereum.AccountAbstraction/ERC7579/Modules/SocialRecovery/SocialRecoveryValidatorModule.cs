using System;
using System.Numerics;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery
{
    public sealed class SocialRecoveryValidatorModule : IErc7579ValidatorModule
    {
        private const int ColdSloadGasDelta = 2000;

        public string Address { get; }

        public int Threshold { get; }

        public SocialRecoveryValidatorModule(string address, int threshold)
        {
            if (string.IsNullOrEmpty(address) || !address.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("Address must be a valid 20-byte hex address", nameof(address));

            if (threshold <= 0)
                throw new ArgumentException($"Threshold must be at least 1 (was {threshold})", nameof(threshold));

            Address = address;
            Threshold = threshold;
        }

        public byte[] ApplySignaturePrefix(byte[] signature) =>
            ByteUtil.Merge(Address.HexToByteArray(), signature);

        public byte[] GetEstimationStubSignature()
        {
            var slots = new byte[Threshold][];
            for (var i = 0; i < Threshold; i++)
                slots[i] = EcdsaValidatorModule.EstimationDummySignature;

            return ByteUtil.Merge(slots);
        }

        public BigInteger GetVerificationGasBuffer() => (Threshold - 1) * ColdSloadGasDelta;
    }
}
