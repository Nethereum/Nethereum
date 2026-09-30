using System;
using System.Numerics;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public sealed class OwnableValidatorSessionValidatorModule : IErc7579ValidatorModule
    {
        private const byte UseMode = 0x00;

        private readonly byte[] _permissionId;
        private readonly int _signatureSlotCount;

        public string Address { get; }

        public OwnableValidatorSessionValidatorModule(string smartSessionAddress, byte[] permissionId, int signatureSlotCount)
        {
            if (string.IsNullOrEmpty(smartSessionAddress) || !smartSessionAddress.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("Address must be a valid 20-byte hex address", nameof(smartSessionAddress));

            if (permissionId == null)
                throw new ArgumentNullException(nameof(permissionId));

            if (permissionId.Length != 32)
                throw new ArgumentException(
                    $"PermissionId must be exactly 32 bytes (was {permissionId.Length})", nameof(permissionId));

            if (signatureSlotCount <= 0)
                throw new ArgumentException(
                    $"SignatureSlotCount must be at least 1 (was {signatureSlotCount})", nameof(signatureSlotCount));

            Address = smartSessionAddress;
            _permissionId = permissionId;
            _signatureSlotCount = signatureSlotCount;
        }

        public byte[] ApplySignaturePrefix(byte[] signature) =>
            ByteUtil.Merge(Address.HexToByteArray(), signature);

        public byte[] GetEstimationStubSignature()
        {
            var slots = new byte[_signatureSlotCount][];
            for (var i = 0; i < _signatureSlotCount; i++)
                slots[i] = EcdsaValidatorModule.EstimationDummySignature;

            return ByteUtil.Merge(new[] { UseMode }, _permissionId, ByteUtil.Merge(slots));
        }

        public BigInteger GetVerificationGasBuffer() => BigInteger.Zero;
    }
}
