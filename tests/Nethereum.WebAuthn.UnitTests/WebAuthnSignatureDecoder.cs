using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.WebAuthn.UnitTests
{
    public static class WebAuthnSignatureDecoder
    {
        [FunctionOutput]
        public class DecodedAuth
        {
            [Parameter("bytes", "authenticatorData", 1)] public byte[] AuthenticatorData { get; set; } = null!;
            [Parameter("string", "clientDataJSON", 2)] public string ClientDataJSON { get; set; } = "";
            [Parameter("uint256", "challengeIndex", 3)] public BigInteger ChallengeIndex { get; set; }
            [Parameter("uint256", "typeIndex", 4)] public BigInteger TypeIndex { get; set; }
            [Parameter("bytes32", "r", 5)] public byte[] R { get; set; } = null!;
            [Parameter("bytes32", "s", 6)] public byte[] S { get; set; } = null!;
        }

        [FunctionOutput]
        public class DecodedSignature
        {
            [Parameter("bytes32[]", "credentialIds", 1)] public List<byte[]> CredentialIds { get; set; } = new();
            [Parameter("bool", "usePrecompile", 2)] public bool UsePrecompile { get; set; }
            [Parameter("tuple[]", "auth", 3)] public List<DecodedAuth> Auth { get; set; } = new();
        }

        public static DecodedSignature Decode(byte[] encoded) =>
            new FunctionCallDecoder().DecodeFunctionOutput<DecodedSignature>(encoded.ToHex(true));

        [FunctionOutput]
        public class DecodedCredential
        {
            [Parameter("uint256", "pubKeyX", 1)] public BigInteger PubKeyX { get; set; }
            [Parameter("uint256", "pubKeyY", 2)] public BigInteger PubKeyY { get; set; }
            [Parameter("bool", "requireUV", 3)] public bool RequireUV { get; set; }
        }

        [FunctionOutput]
        public class DecodedInstall
        {
            [Parameter("uint256", "threshold", 1)] public BigInteger Threshold { get; set; }
            [Parameter("tuple[]", "credentials", 2)] public List<DecodedCredential> Credentials { get; set; } = new();
        }

        public static DecodedInstall DecodeInstall(byte[] encoded) =>
            new FunctionCallDecoder().DecodeFunctionOutput<DecodedInstall>(encoded.ToHex(true));
    }
}
