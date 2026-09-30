using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.Signing
{
    public interface IErc7579ValidatorModule
    {
        string Address { get; }

        byte[] ApplySignaturePrefix(byte[] signature);

        byte[] GetEstimationStubSignature();

        BigInteger GetVerificationGasBuffer();
    }

    public sealed class EcdsaValidatorModule : IErc7579ValidatorModule
    {
        internal static readonly byte[] EstimationDummySignature =
            "0xfffffffffffffffffffffffffffffff0000000000000000000000000000000007aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1c"
                .HexToByteArray();

        public string Address { get; }

        public EcdsaValidatorModule(string address)
        {
            Address = address;
        }

        public byte[] ApplySignaturePrefix(byte[] signature) =>
            ApplySignaturePrefix(Address, signature);

        public byte[] GetEstimationStubSignature() => EstimationDummySignature;

        public BigInteger GetVerificationGasBuffer() => BigInteger.Zero;

        public static byte[] ApplySignaturePrefix(string validatorAddress, byte[] signature) =>
            ByteUtil.Merge(validatorAddress.HexToByteArray(), signature);
    }
}
