using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer.EIP712;
using Nethereum.Util;

namespace Nethereum.WebAuthn
{
    public class WebAuthnSignTypedDataV4 : IEthSignTypedDataV4
    {
        private readonly IWebAuthnAuthenticator _authenticator;
        private readonly byte[] _credentialId;
        private readonly byte[] _platformCredentialId;
        private readonly string _rpId;
        private readonly bool _usePrecompile;
        private readonly Eip712TypedDataSigner _typedDataSigner = new Eip712TypedDataSigner();

        public WebAuthnSignTypedDataV4(IWebAuthnAuthenticator authenticator, byte[] credentialId, string rpId, bool usePrecompile = false, byte[] platformCredentialId = null)
        {
            _authenticator = authenticator;
            _credentialId = credentialId;
            _platformCredentialId = platformCredentialId ?? credentialId;
            _rpId = rpId;
            _usePrecompile = usePrecompile;
        }

        public async Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            var encodedData = _typedDataSigner.EncodeTypedData(jsonMessage);
            var challenge = Sha3Keccack.Current.CalculateHash(encodedData);
            var assertion = await _authenticator.GetAssertionAsync(challenge, _platformCredentialId, _rpId).ConfigureAwait(false);
            var encoded = WebAuthnValidatorFormat.EncodeUserOpSignature(_credentialId, _usePrecompile, assertion);
            return encoded.ToHex(true);
        }

        public RpcRequest BuildRequest(string message, object id = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// WebAuthn has no equivalent of EIP-191 <c>personal_sign</c> over an arbitrary raw digest - the
    /// challenge must be carried inside <c>clientDataJSON</c> and produced by an authenticator
    /// assertion, not a bare hash signature. Not supported by this signer.
    /// </summary>
    public class WebAuthnPersonalSign : IEthPersonalSign
    {
        public Task<string> SendRequestAsync(byte[] value, object id = null)
        {
            throw new NotSupportedException("WebAuthn does not support personal_sign; use SignTypedDataV4.");
        }

        public Task<string> SendRequestAsync(HexUTF8String utf8Hex, object id = null)
        {
            throw new NotSupportedException("WebAuthn does not support personal_sign; use SignTypedDataV4.");
        }

        public RpcRequest BuildRequest(byte[] value, object id = null)
        {
            throw new NotImplementedException();
        }

        public RpcRequest BuildRequest(HexUTF8String utf8Hex, object id = null)
        {
            throw new NotImplementedException();
        }
    }

    public class WebAuthnAccountSigningService : IAccountSigningService
    {
        public IEthSignTypedDataV4 SignTypedDataV4 { get; }
        public IEthPersonalSign PersonalSign { get; }

        public WebAuthnAccountSigningService(IWebAuthnAuthenticator authenticator, byte[] credentialId, string rpId, bool usePrecompile = false, byte[] platformCredentialId = null)
        {
            SignTypedDataV4 = new WebAuthnSignTypedDataV4(authenticator, credentialId, rpId, usePrecompile, platformCredentialId);
            PersonalSign = new WebAuthnPersonalSign();
        }
    }
}
