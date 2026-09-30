using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.WebAuthn.Blazor.UnitTests
{
    public class BlazorWebAuthnAuthenticatorTests
    {
        [Fact]
        public async Task CreateCredentialAsync_uses_the_SPKI_public_key_when_present()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var spki = key.ExportSubjectPublicKeyInfo();
            var parameters = key.ExportParameters(false);
            var expectedX = new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true);
            var expectedY = new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true);
            var rawId = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };

            var jsRuntime = new FakeJSRuntime();
            string? capturedRequestJson = null;
            jsRuntime.Module.OnInvoke = (identifier, args) =>
            {
                Assert.Equal("createCredential", identifier);
                capturedRequestJson = (string)args![0]!;
                return JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["rawId"] = Base64UrlEncoder.Encode(rawId),
                    ["publicKeySpki"] = Convert.ToBase64String(spki),
                    ["authenticatorData"] = null,
                    ["attestationObject"] = Convert.ToBase64String(new byte[] { 0x00 })
                });
            };

            var authenticator = new BlazorWebAuthnAuthenticator(jsRuntime);
            var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
            {
                RpId = "nethereum.local",
                RpName = "Nethereum Demo",
                UserName = "alice",
                RequireUserVerification = true
            });

            Assert.Equal(expectedX, created.PubKeyX);
            Assert.Equal(expectedY, created.PubKeyY);
            Assert.Equal(rawId, created.PlatformCredentialId);
            Assert.True(created.RequireUserVerification);

            using var request = JsonDocument.Parse(capturedRequestJson!);
            var root = request.RootElement;
            Assert.Equal("nethereum.local", root.GetProperty("rpId").GetString());
            Assert.Equal("Nethereum Demo", root.GetProperty("rpName").GetString());
            Assert.Equal("alice", root.GetProperty("userName").GetString());
            Assert.True(root.GetProperty("requireUserVerification").GetBoolean());
            Assert.Equal(16, Base64UrlEncoder.Decode(root.GetProperty("userIdB64Url").GetString()!).Length);
            Assert.Equal(32, Base64UrlEncoder.Decode(root.GetProperty("challengeB64Url").GetString()!).Length);
        }

        [Fact]
        public async Task CreateCredentialAsync_falls_back_to_the_COSE_key_in_authenticatorData_when_SPKI_is_null()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = key.ExportParameters(false);
            var expectedX = new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true);
            var expectedY = new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true);

            var credentialId = new byte[] { 1, 2, 3, 4 };
            var cosePublicKey = EncodeCoseP256Key(expectedX, expectedY);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, cosePublicKey);

            var jsRuntime = new FakeJSRuntime();
            jsRuntime.Module.OnInvoke = (identifier, args) => JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["rawId"] = Base64UrlEncoder.Encode(credentialId),
                ["publicKeySpki"] = null,
                ["authenticatorData"] = Convert.ToBase64String(authenticatorData),
                ["attestationObject"] = Convert.ToBase64String(new byte[] { 0x00 })
            });

            var authenticator = new BlazorWebAuthnAuthenticator(jsRuntime);
            var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions { RpId = "nethereum.local" });

            Assert.Equal(expectedX, created.PubKeyX);
            Assert.Equal(expectedY, created.PubKeyY);
        }

        [Fact]
        public async Task CreateCredentialAsync_falls_back_to_the_attestationObject_when_SPKI_and_authenticatorData_are_both_null()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = key.ExportParameters(false);
            var expectedX = new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true);
            var expectedY = new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true);

            var credentialId = new byte[] { 5, 6, 7, 8 };
            var cosePublicKey = EncodeCoseP256Key(expectedX, expectedY);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, cosePublicKey);
            var attestationObject = EncodeAttestationObject(authenticatorData);

            var jsRuntime = new FakeJSRuntime();
            jsRuntime.Module.OnInvoke = (identifier, args) => JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["rawId"] = Base64UrlEncoder.Encode(credentialId),
                ["publicKeySpki"] = null,
                ["authenticatorData"] = null,
                ["attestationObject"] = Convert.ToBase64String(attestationObject)
            });

            var authenticator = new BlazorWebAuthnAuthenticator(jsRuntime);
            var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions { RpId = "nethereum.local" });

            Assert.Equal(expectedX, created.PubKeyX);
            Assert.Equal(expectedY, created.PubKeyY);
        }

        [Fact]
        public async Task GetAssertionAsync_returns_an_assertion_whose_signature_verifies_against_the_key()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var challenge = SHA256.HashData(Encoding.UTF8.GetBytes("userOpHash"));
            var credentialId = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            const string rpId = "nethereum.local";

            var authenticatorData = BuildPlainAuthenticatorData(rpId);
            var clientDataJSON = "{\"type\":\"webauthn.get\",\"challenge\":\"" +
                Base64UrlEncoder.Encode(challenge) + "\",\"origin\":\"https://nethereum.local\"}";
            var digest = DigestOf(authenticatorData, clientDataJSON);
            var derSignature = key.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence);

            var jsRuntime = new FakeJSRuntime();
            object?[]? capturedArgs = null;
            jsRuntime.Module.OnInvoke = (identifier, args) =>
            {
                Assert.Equal("getAssertion", identifier);
                capturedArgs = args;
                return JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["authenticatorData"] = Convert.ToBase64String(authenticatorData),
                    ["clientDataJSON"] = clientDataJSON,
                    ["signature"] = Convert.ToBase64String(derSignature),
                    ["userHandle"] = null
                });
            };

            var authenticator = new BlazorWebAuthnAuthenticator(jsRuntime, requireUserVerificationForAssertion: true);
            var assertion = await authenticator.GetAssertionAsync(challenge, credentialId, rpId);

            Assert.Equal(credentialId, assertion.CredentialId);
            Assert.Equal(authenticatorData, assertion.AuthenticatorData);
            Assert.Equal(clientDataJSON, assertion.ClientDataJSON);
            Assert.Equal(32, assertion.R.Length);
            Assert.Equal(32, assertion.S.Length);
            Assert.True(Verify(key, authenticatorData, clientDataJSON, assertion.R, assertion.S));

            Assert.Equal(challenge, Base64UrlEncoder.Decode((string)capturedArgs![0]!));
            Assert.Equal(credentialId, Base64UrlEncoder.Decode((string)capturedArgs[1]!));
            Assert.Equal(rpId, capturedArgs[2]);
            Assert.Equal(true, capturedArgs[3]);
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

        private static byte[] EncodeAttestationObject(byte[] authData)
        {
            var writer = new CborWriter();
            writer.WriteStartMap(3);
            writer.WriteTextString("fmt");
            writer.WriteTextString("none");
            writer.WriteTextString("attStmt");
            writer.WriteStartMap(0);
            writer.WriteEndMap();
            writer.WriteTextString("authData");
            writer.WriteByteString(authData);
            writer.WriteEndMap();
            return writer.Encode();
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
