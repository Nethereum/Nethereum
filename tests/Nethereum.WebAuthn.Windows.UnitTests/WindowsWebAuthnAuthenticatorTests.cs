using System;
using System.Formats.Asn1;
using System.Formats.Cbor;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.WebAuthn;
using Nethereum.WebAuthn.Windows;
using Xunit;

namespace Nethereum.WebAuthn.Windows.UnitTests
{
    public class WindowsWebAuthnAuthenticatorTests
    {
        private static readonly BigInteger P256HalfOrder = BigInteger.Parse(
            "57896044605178124381348723474703786764998477612067880171211129530534256022184");

        [Fact]
        public void BuildCreateClientDataJson_has_the_create_type_and_round_trips_the_challenge_and_origin()
        {
            var challenge = SHA256.HashData(Encoding.UTF8.GetBytes("registration-challenge"));

            var json = WebAuthnNativeBufferMapping.BuildCreateClientDataJson(challenge, "https://nethereum.local");

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.Equal("webauthn.create", root.GetProperty("type").GetString());
            Assert.Equal("https://nethereum.local", root.GetProperty("origin").GetString());
            Assert.Equal(challenge, Base64UrlEncoder.Decode(root.GetProperty("challenge").GetString()!));
        }

        [Fact]
        public void BuildGetClientDataJson_has_the_get_type_and_round_trips_the_challenge_and_origin()
        {
            var challenge = SHA256.HashData(Encoding.UTF8.GetBytes("userOpHash"));

            var json = WebAuthnNativeBufferMapping.BuildGetClientDataJson(challenge, "https://nethereum.local");

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.Equal("webauthn.get", root.GetProperty("type").GetString());
            Assert.Equal("https://nethereum.local", root.GetProperty("origin").GetString());
            Assert.Equal(challenge, Base64UrlEncoder.Decode(root.GetProperty("challenge").GetString()!));
        }

        [Theory]
        [InlineData("nethereum.local", "https://nethereum.local")]
        [InlineData("example.com", "https://example.com")]
        public void OriginFor_derives_a_stable_https_origin_from_the_rpId(string rpId, string expectedOrigin)
        {
            Assert.Equal(expectedOrigin, WebAuthnNativeBufferMapping.OriginFor(rpId));
        }

        [Fact]
        public void MapCreatedCredential_extracts_the_public_key_from_the_native_authenticatorData_buffer()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = key.ExportParameters(false);
            var expectedX = new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true);
            var expectedY = new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true);

            var credentialId = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var cosePublicKey = EncodeCoseP256Key(expectedX, expectedY);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, cosePublicKey);

            var created = WebAuthnNativeBufferMapping.MapCreatedCredential(authenticatorData, credentialId, requireUserVerification: true);

            Assert.Equal(expectedX, created.PubKeyX);
            Assert.Equal(expectedY, created.PubKeyY);
            Assert.Equal(credentialId, created.PlatformCredentialId);
            Assert.True(created.RequireUserVerification);
        }

        [Fact]
        public void MapAssertion_decodes_the_native_DER_signature_to_low_S_and_it_verifies_against_the_key()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            const string rpId = "nethereum.local";
            var challenge = SHA256.HashData(Encoding.UTF8.GetBytes("userOpHash"));
            var credentialId = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            var authenticatorData = BuildPlainAuthenticatorData(rpId);
            var clientDataJson = WebAuthnNativeBufferMapping.BuildGetClientDataJson(challenge, WebAuthnNativeBufferMapping.OriginFor(rpId));
            var digest = DigestOf(authenticatorData, clientDataJson);
            var derSignature = key.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence);

            var assertion = WebAuthnNativeBufferMapping.MapAssertion(credentialId, authenticatorData, clientDataJson, derSignature);

            Assert.Equal(credentialId, assertion.CredentialId);
            Assert.Equal(authenticatorData, assertion.AuthenticatorData);
            Assert.Equal(clientDataJson, assertion.ClientDataJSON);
            Assert.Equal(32, assertion.R.Length);
            Assert.Equal(32, assertion.S.Length);
            Assert.True(Verify(key, authenticatorData, clientDataJson, assertion.R, assertion.S));
        }

        [Fact]
        public void MapAssertion_normalizes_a_forced_high_S_native_signature_to_low_S_and_it_still_verifies()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var authenticatorData = BuildPlainAuthenticatorData("nethereum.local");
            var clientDataJson = "{\"type\":\"webauthn.get\",\"challenge\":\"x\",\"origin\":\"https://nethereum.local\"}";
            var digest = DigestOf(authenticatorData, clientDataJson);

            var ieeeSignature = key.SignHash(digest, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var r = new BigInteger(ieeeSignature.AsSpan(0, 32), isUnsigned: true, isBigEndian: true);
            var s = new BigInteger(ieeeSignature.AsSpan(32, 32), isUnsigned: true, isBigEndian: true);
            var order = P256HalfOrder * 2 + 1;
            var highS = order - s;
            if (highS <= P256HalfOrder) highS = order - highS;
            var forcedHighSDer = DerEncode(r, highS);

            var assertion = WebAuthnNativeBufferMapping.MapAssertion(Array.Empty<byte>(), authenticatorData, clientDataJson, forcedHighSDer);

            var decodedS = new BigInteger(assertion.S, isUnsigned: true, isBigEndian: true);
            Assert.True(decodedS <= P256HalfOrder, "s must be normalized to low-S");
            Assert.Equal(order - highS, decodedS);
            Assert.True(Verify(key, authenticatorData, clientDataJson, assertion.R, assertion.S));
        }

        // RFC 9053 §7.1 COSE_Key EC2 map: kty(1)=2, crv(-1)=1 (P-256), x(-2)/y(-3).
        private static byte[] EncodeCoseP256Key(BigInteger x, BigInteger y)
        {
            var writer = new CborWriter();
            writer.WriteStartMap(4);
            writer.WriteInt32(1); writer.WriteInt32(2);
            writer.WriteInt32(-1); writer.WriteInt32(1);
            writer.WriteInt32(-2); writer.WriteByteString(To32(x));
            writer.WriteInt32(-3); writer.WriteByteString(To32(y));
            writer.WriteEndMap();
            return writer.Encode();
        }

        private static byte[] BuildAuthenticatorDataWithAttestedCredential(byte[] credentialId, byte[] cosePublicKey)
        {
            var rpIdHash = new byte[32];
            var flags = (byte)0x41;
            var signCount = new byte[] { 0, 0, 0, 1 };
            var aaguid = new byte[16];
            var credentialIdLength = new byte[] { (byte)(credentialId.Length >> 8), (byte)credentialId.Length };

            using var stream = new System.IO.MemoryStream();
            stream.Write(rpIdHash);
            stream.WriteByte(flags);
            stream.Write(signCount);
            stream.Write(aaguid);
            stream.Write(credentialIdLength);
            stream.Write(credentialId);
            stream.Write(cosePublicKey);
            return stream.ToArray();
        }

        private static byte[] BuildPlainAuthenticatorData(string rpId)
        {
            var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
            var flags = (byte)0x01;
            var signCount = new byte[] { 0, 0, 0, 1 };

            using var stream = new System.IO.MemoryStream();
            stream.Write(rpIdHash);
            stream.WriteByte(flags);
            stream.Write(signCount);
            return stream.ToArray();
        }

        private static byte[] DigestOf(byte[] authenticatorData, string clientDataJson)
        {
            var clientDataHash = SHA256.HashData(Encoding.UTF8.GetBytes(clientDataJson));
            var signed = new byte[authenticatorData.Length + clientDataHash.Length];
            Buffer.BlockCopy(authenticatorData, 0, signed, 0, authenticatorData.Length);
            Buffer.BlockCopy(clientDataHash, 0, signed, authenticatorData.Length, clientDataHash.Length);
            return SHA256.HashData(signed);
        }

        private static bool Verify(ECDsa key, byte[] authenticatorData, string clientDataJson, byte[] r, byte[] s)
        {
            var digest = DigestOf(authenticatorData, clientDataJson);
            var signature = new byte[64];
            Buffer.BlockCopy(r, 0, signature, 0, 32);
            Buffer.BlockCopy(s, 0, signature, 32, 32);
            return key.VerifyHash(digest, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        private static byte[] DerEncode(BigInteger r, BigInteger s)
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                writer.WriteInteger(r);
                writer.WriteInteger(s);
            }
            return writer.Encode();
        }

        private static byte[] To32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }
    }
}
