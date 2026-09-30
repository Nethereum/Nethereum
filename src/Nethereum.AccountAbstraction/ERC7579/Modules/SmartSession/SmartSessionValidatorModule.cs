using System;
using System.Numerics;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public sealed class SmartSessionValidatorModule : IErc7579ValidatorModule
    {
        private const byte UseMode = 0x00;

        private readonly byte[] _permissionId;

        public string Address { get; }

        public SmartSessionValidatorModule(string address, byte[] permissionId)
        {
            if (string.IsNullOrEmpty(address) || !address.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("Address must be a valid 20-byte hex address", nameof(address));

            if (permissionId == null)
                throw new ArgumentNullException(nameof(permissionId));

            if (permissionId.Length != 32)
                throw new ArgumentException(
                    $"PermissionId must be exactly 32 bytes (was {permissionId.Length})", nameof(permissionId));

            Address = address;
            _permissionId = permissionId;
        }

        public byte[] ApplySignaturePrefix(byte[] signature) =>
            ByteUtil.Merge(Address.HexToByteArray(), signature);

        public byte[] GetEstimationStubSignature() =>
            ByteUtil.Merge(new[] { UseMode }, _permissionId, EcdsaValidatorModule.EstimationDummySignature);

        public BigInteger GetVerificationGasBuffer() => BigInteger.Zero;
    }
}
