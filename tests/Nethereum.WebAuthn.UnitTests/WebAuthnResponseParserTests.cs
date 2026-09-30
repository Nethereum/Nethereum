using System;
using System.Formats.Asn1;
using System.Formats.Cbor;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class WebAuthnResponseParserTests
    {
        private static readonly BigInteger P256HalfOrder = BigInteger.Parse(
            "57896044605178124381348723474703786764998477612067880171211129530534256022184");

        [Fact]
        public void Decodes_a_freshly_signed_DER_signature_and_it_still_verifies()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var hash = SHA256.HashData(new byte[] { 1, 2, 3, 4, 5 });
            var der = ecdsa.SignHash(hash, DSASignatureFormat.Rfc3279DerSequence);

            var (r, s) = WebAuthnResponseParser.DecodeDerEcdsaSignatureToLowS(der);

            Assert.Equal(32, r.Length);
            Assert.Equal(32, s.Length);
            var sValue = new BigInteger(s, isUnsigned: true, isBigEndian: true);
            Assert.True(sValue <= P256HalfOrder, "s must be normalized to low-S");

            var ieee = new byte[64];
            Buffer.BlockCopy(r, 0, ieee, 0, 32);
            Buffer.BlockCopy(s, 0, ieee, 32, 32);
            Assert.True(ecdsa.VerifyHash(hash, ieee, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }

        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void Decodes_a_DER_encoding_of_the_reference_vectors_back_to_the_original_r_s(WebAuthnReferenceVectors.Vector v)
        {
            var der = DerEncode(v.R, v.S);

            var (r, s) = WebAuthnResponseParser.DecodeDerEcdsaSignatureToLowS(der);

            Assert.Equal(WebAuthnAssertionCrypto.To32(v.R), r);
            Assert.Equal(WebAuthnAssertionCrypto.To32(v.S), s);
        }

        [Fact]
        public void Normalizes_a_high_S_DER_signature_to_low_S()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var hash = SHA256.HashData(new byte[] { 9, 9, 9 });
            var ieeeSignature = ecdsa.SignHash(hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var r = new BigInteger(ieeeSignature.AsSpan(0, 32), isUnsigned: true, isBigEndian: true);
            var s = new BigInteger(ieeeSignature.AsSpan(32, 32), isUnsigned: true, isBigEndian: true);
            var order = P256HalfOrder * 2 + 1;
            var highS = order - s;
            if (highS <= P256HalfOrder) highS = order - highS;
            var der = DerEncode(r, highS);

            var (_, decodedS) = WebAuthnResponseParser.DecodeDerEcdsaSignatureToLowS(der);

            var decodedValue = new BigInteger(decodedS, isUnsigned: true, isBigEndian: true);
            Assert.True(decodedValue <= P256HalfOrder);
            Assert.Equal(order - highS, decodedValue);
        }

        [Fact]
        public void Decodes_an_SPKI_public_key_matching_ECDsa_ExportParameters()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var spki = ecdsa.ExportSubjectPublicKeyInfo();
            var expected = ecdsa.ExportParameters(false);

            var (x, y) = WebAuthnResponseParser.DecodeP256PublicKeyFromSpki(spki);

            Assert.Equal(new BigInteger(expected.Q.X, isUnsigned: true, isBigEndian: true), x);
            Assert.Equal(new BigInteger(expected.Q.Y, isUnsigned: true, isBigEndian: true), y);
        }

        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void Decodes_a_COSE_EC2_key_for_the_reference_vectors(WebAuthnReferenceVectors.Vector v)
        {
            var cose = EncodeCoseP256Key(v.PubKeyX, v.PubKeyY);

            var (x, y) = WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cose);

            Assert.Equal(v.PubKeyX, x);
            Assert.Equal(v.PubKeyY, y);
        }

        [Fact]
        public void Rejects_a_COSE_key_with_the_wrong_key_type()
        {
            var cose = EncodeCoseKeyRaw(kty: 1, crv: 1, x: new byte[32], y: new byte[32]);

            Assert.Throws<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cose));
        }

        [Fact]
        public void Rejects_a_COSE_key_with_the_wrong_curve()
        {
            var cose = EncodeCoseKeyRaw(kty: 2, crv: 2, x: new byte[32], y: new byte[32]);

            Assert.Throws<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cose));
        }

        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void ExtractCredentialId_and_ExtractCosePublicKey_round_trip_from_authenticatorData(WebAuthnReferenceVectors.Vector v)
        {
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(v.PubKeyX, v.PubKeyY);
            var coseKey = EncodeCoseP256Key(v.PubKeyX, v.PubKeyY);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, coseKey);

            var extractedCredentialId = WebAuthnResponseParser.ExtractCredentialId(authenticatorData);
            var extractedCose = WebAuthnResponseParser.ExtractCosePublicKeyFromAuthenticatorData(authenticatorData);
            var (x, y) = WebAuthnResponseParser.DecodeP256PublicKeyFromCose(extractedCose);

            Assert.Equal(credentialId, extractedCredentialId);
            Assert.Equal(v.PubKeyX, x);
            Assert.Equal(v.PubKeyY, y);
        }

        [Fact]
        public void Throws_when_the_attested_credential_data_flag_is_not_set()
        {
            var credentialId = new byte[] { 1, 2, 3, 4 };
            var coseKey = EncodeCoseP256Key(BigInteger.One, BigInteger.One);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, coseKey, setAtFlag: false);

            Assert.Throws<ArgumentException>(() => WebAuthnResponseParser.ExtractCredentialId(authenticatorData));
        }

        [Fact]
        public void Throws_when_authenticatorData_is_shorter_than_its_declared_credentialIdLength()
        {
            var credentialId = new byte[] { 1, 2, 3, 4 };
            var coseKey = EncodeCoseP256Key(BigInteger.One, BigInteger.One);
            var authenticatorData = BuildAuthenticatorDataWithAttestedCredential(credentialId, coseKey);
            var truncated = authenticatorData[..(authenticatorData.Length - coseKey.Length - 2)];

            Assert.Throws<ArgumentException>(() => WebAuthnResponseParser.ExtractCredentialId(truncated));
        }

        [Fact]
        public void ExtractAuthDataFromAttestationObject_returns_the_authData_byte_string()
        {
            var authData = new byte[] { 10, 20, 30, 40, 50 };
            var attestationObject = BuildAttestationObject("none", authData);

            var extracted = WebAuthnResponseParser.ExtractAuthDataFromAttestationObject(attestationObject);

            Assert.Equal(authData, extracted);
        }

        [Fact]
        public void ExtractAuthDataFromAttestationObject_throws_when_authData_is_absent()
        {
            var writer = new CborWriter();
            writer.WriteStartMap(1);
            writer.WriteTextString("fmt");
            writer.WriteTextString("none");
            writer.WriteEndMap();
            var attestationObject = writer.Encode();

            Assert.Throws<InvalidOperationException>(() => WebAuthnResponseParser.ExtractAuthDataFromAttestationObject(attestationObject));
        }

        private static byte[] BuildAttestationObject(string fmt, byte[] authData)
        {
            var writer = new CborWriter();
            writer.WriteStartMap(3);
            writer.WriteTextString("fmt");
            writer.WriteTextString(fmt);
            writer.WriteTextString("attStmt");
            writer.WriteStartMap(0);
            writer.WriteEndMap();
            writer.WriteTextString("authData");
            writer.WriteByteString(authData);
            writer.WriteEndMap();
            return writer.Encode();
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

        private static byte[] EncodeCoseP256Key(BigInteger x, BigInteger y) =>
            EncodeCoseKeyRaw(kty: 2, crv: 1, x: WebAuthnAssertionCrypto.To32(x), y: WebAuthnAssertionCrypto.To32(y));

        // RFC 9053 §7.1 COSE_Key EC2 map: kty(1), alg(3, optional), crv(-1), x(-2), y(-3).
        private static byte[] EncodeCoseKeyRaw(int kty, int crv, byte[] x, byte[] y)
        {
            var writer = new CborWriter();
            writer.WriteStartMap(5);
            writer.WriteInt32(1); writer.WriteInt32(kty);
            writer.WriteInt32(3); writer.WriteInt32(-7);
            writer.WriteInt32(-1); writer.WriteInt32(crv);
            writer.WriteInt32(-2); writer.WriteByteString(x);
            writer.WriteInt32(-3); writer.WriteByteString(y);
            writer.WriteEndMap();
            return writer.Encode();
        }

        private static byte[] BuildAuthenticatorDataWithAttestedCredential(byte[] credentialId, byte[] cosePublicKey, bool setAtFlag = true)
        {
            var rpIdHash = new byte[32];
            for (var i = 0; i < rpIdHash.Length; i++) rpIdHash[i] = (byte)i;
            var flags = (byte)(0x01 | (setAtFlag ? 0x40 : 0x00));
            var signCount = new byte[] { 0, 0, 0, 1 };
            var aaguid = new byte[16];
            var credentialIdLength = new byte[] { (byte)(credentialId.Length >> 8), (byte)credentialId.Length };

            using var stream = new MemoryStream();
            stream.Write(rpIdHash, 0, rpIdHash.Length);
            stream.WriteByte(flags);
            stream.Write(signCount, 0, signCount.Length);
            stream.Write(aaguid, 0, aaguid.Length);
            stream.Write(credentialIdLength, 0, credentialIdLength.Length);
            stream.Write(credentialId, 0, credentialId.Length);
            stream.Write(cosePublicKey, 0, cosePublicKey.Length);
            return stream.ToArray();
        }
    }
}
