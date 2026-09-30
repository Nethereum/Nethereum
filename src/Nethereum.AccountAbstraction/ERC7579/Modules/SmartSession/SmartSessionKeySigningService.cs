using System;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public sealed class SmartSessionKeySignTypedDataV4 : IEthSignTypedDataV4
    {
        private const byte UseMode = 0x00;

        private readonly EthSignTypedDataV4Offline _inner;
        private readonly byte[] _permissionId;

        public SmartSessionKeySignTypedDataV4(EthECKey sessionKey, byte[] permissionId)
        {
            if (sessionKey == null)
                throw new ArgumentNullException(nameof(sessionKey));

            if (permissionId == null)
                throw new ArgumentNullException(nameof(permissionId));

            if (permissionId.Length != 32)
                throw new ArgumentException(
                    $"PermissionId must be exactly 32 bytes (was {permissionId.Length})", nameof(permissionId));

            _inner = new EthSignTypedDataV4Offline(sessionKey);
            _permissionId = permissionId;
        }

        public async Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            var rawSignature = (await _inner.SendRequestAsync(jsonMessage, id).ConfigureAwait(false)).HexToByteArray();
            var wrapped = ByteUtil.Merge(new[] { UseMode }, _permissionId, rawSignature);
            return wrapped.ToHex(true);
        }

        public RpcRequest BuildRequest(string message, object id = null) => _inner.BuildRequest(message, id);
    }

    public sealed class SmartSessionKeySigningService : IAccountSigningService
    {
        public IEthSignTypedDataV4 SignTypedDataV4 { get; }
        public IEthPersonalSign PersonalSign { get; }

        public SmartSessionKeySigningService(EthECKey sessionKey, byte[] permissionId)
        {
            SignTypedDataV4 = new SmartSessionKeySignTypedDataV4(sessionKey, permissionId);
            PersonalSign = new UnsupportedSessionKeyPersonalSign();
        }

        private sealed class UnsupportedSessionKeyPersonalSign : IEthPersonalSign
        {
            private const string UnsupportedMessage =
                "SmartSession session-key ERC-1271 signing requires the ERC-7739 nested-EIP-712 format " +
                "(isValidSignatureWithSender), which this service does not implement; use SignTypedDataV4 " +
                "for UserOperation signing.";

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
