using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public sealed class OwnableValidatorSessionSignTypedDataV4 : IEthSignTypedDataV4
    {
        private const byte UseMode = 0x00;

        private readonly byte[] _permissionId;
        private readonly Func<byte[], byte[]> _buildBlob;
        private readonly Eip712TypedDataSigner _typedDataSigner = new Eip712TypedDataSigner();

        public OwnableValidatorSessionSignTypedDataV4(byte[] permissionId, Func<byte[], byte[]> buildBlob)
        {
            if (permissionId == null)
                throw new ArgumentNullException(nameof(permissionId));

            if (permissionId.Length != 32)
                throw new ArgumentException(
                    $"PermissionId must be exactly 32 bytes (was {permissionId.Length})", nameof(permissionId));

            if (buildBlob == null)
                throw new ArgumentNullException(nameof(buildBlob));

            _permissionId = permissionId;
            _buildBlob = buildBlob;
        }

        public Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            var encodedData = _typedDataSigner.EncodeTypedData(jsonMessage);
            var userOpHash = Sha3Keccack.Current.CalculateHash(encodedData);
            var blob = _buildBlob(userOpHash);
            var wrapped = ByteUtil.Merge(new[] { UseMode }, _permissionId, blob);
            return Task.FromResult(wrapped.ToHex(true));
        }

        public RpcRequest BuildRequest(string message, object id = null) => throw new NotImplementedException();
    }

    public sealed class OwnableValidatorSessionSigningService : IAccountSigningService
    {
        public IEthSignTypedDataV4 SignTypedDataV4 { get; }
        public IEthPersonalSign PersonalSign { get; } = new UnsupportedPersonalSign();

        public OwnableValidatorSessionSigningService(byte[] permissionId, IReadOnlyList<EthECKey> signers, int threshold)
            : this(permissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, signers, threshold))
        {
            MultiGuardianSignatureBlobBuilder.ValidateGuardianSet(signers, threshold);
        }

        public OwnableValidatorSessionSigningService(byte[] permissionId, Func<byte[], byte[]> buildBlob)
        {
            SignTypedDataV4 = new OwnableValidatorSessionSignTypedDataV4(permissionId, buildBlob);
        }

        private sealed class UnsupportedPersonalSign : IEthPersonalSign
        {
            private const string UnsupportedMessage =
                "OwnableValidator SmartSession N-of-M authority signing only supports UserOperation " +
                "signing (SignTypedDataV4); ERC-1271 personal-sign is not supported.";

            public Task<string> SendRequestAsync(byte[] value, object id = null) =>
                throw new NotSupportedException(UnsupportedMessage);

            public Task<string> SendRequestAsync(HexUTF8String utf8Hex, object id = null) =>
                throw new NotSupportedException(UnsupportedMessage);

            public RpcRequest BuildRequest(byte[] value, object id = null) =>
                throw new NotSupportedException(UnsupportedMessage);

            public RpcRequest BuildRequest(HexUTF8String utf8Hex, object id = null) =>
                throw new NotSupportedException(UnsupportedMessage);
        }
    }
}
