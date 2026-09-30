using System.Linq;
using System.Numerics;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.WebAuthn;

namespace Nethereum.AccountAbstraction.WebAuthn.Signing
{
    public sealed class WebAuthnValidatorModule : IErc7579ValidatorModule
    {
        private static readonly byte[] EstimationDummyCredentialId =
            "0x1111111111111111111111111111111111111111111111111111111111111111".HexToByteArray();

        private static readonly byte[] EstimationDummyChallenge =
            Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

        private static readonly byte[] EstimationDummyAuthenticatorData =
            "0x49960de5880e8c687434170f6476605b8fe4aeb9a28632c7995cf3ba831d97630100000001".HexToByteArray();

        private static readonly byte[] EstimationDummyR =
            "0x33fab625d140dc53c6903ee49f3f5527144c7349a2744f3191744c5a422a41d8".HexToByteArray();

        private static readonly byte[] EstimationDummyS =
            "0x50e5a1117e0432ab5f1552921e5fd1c046bf1475a8a961bdd5fd30068a6484e0".HexToByteArray();

        // The stub's challenge can never equal the real userOpHash, so on-chain WebAuthn.verify
        // (webauthn-sol) short-circuits at the challenge check BEFORE the SHA-256 hashing and the P-256
        // signature verification. (The credential's public key is loaded by the on-chain validator
        // before verify runs - unconditionally, on both the stub and the real path - so it is NOT the
        // omitted cost.) The dummy-signature estimate therefore omits the verification work, which these
        // buffers add back. That omitted cost is per-signature, so it is independent of how many other
        // validators the account has: with threshold 1, exactly one signature is verified either way.
        //   - EIP-7951 P256VERIFY precompile: cheap (the staticcall + hashing); 15k is ample headroom.
        //   - FreshCryptoLib pure-Solidity fallback: dominated by the on-chain ecdsa_verify, so it needs
        //     a large buffer. 400k is proven sufficient by WebAuthnSecondValidatorE2ETests, which lands a
        //     post-deployment passkey-signed op through the on-ramp with NO gas override (both paths).
        private const long PrecompileVerificationGasBuffer = 15_000;
        private const long FallbackVerificationGasBuffer = 400_000;

        private readonly bool _usePrecompile;

        public string Address { get; }

        public WebAuthnValidatorModule(string address, bool usePrecompile = false)
        {
            Address = address;
            _usePrecompile = usePrecompile;
        }

        public byte[] ApplySignaturePrefix(byte[] signature) =>
            ByteUtil.Merge(Address.HexToByteArray(), signature);

        public byte[] GetEstimationStubSignature()
        {
            var clientDataJSON =
                "{\"type\":\"webauthn.get\",\"challenge\":\"" + Base64UrlEncoder.Encode(EstimationDummyChallenge) +
                "\",\"origin\":\"http://localhost:8080\",\"crossOrigin\":false}";

            var dummyAssertion = new WebAuthnAssertion
            {
                CredentialId = EstimationDummyCredentialId,
                AuthenticatorData = EstimationDummyAuthenticatorData,
                ClientDataJSON = clientDataJSON,
                R = EstimationDummyR,
                S = EstimationDummyS
            };

            return WebAuthnValidatorFormat.EncodeUserOpSignature(
                new[] { EstimationDummyCredentialId }, _usePrecompile, new[] { dummyAssertion });
        }

        public BigInteger GetVerificationGasBuffer() =>
            _usePrecompile ? PrecompileVerificationGasBuffer : FallbackVerificationGasBuffer;
    }
}
