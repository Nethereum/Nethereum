using System;
using System.Formats.Asn1;
using System.Formats.Cbor;
using System.Numerics;
using System.Security.Cryptography;

namespace Nethereum.WebAuthn
{
    public static class WebAuthnResponseParser
    {
        internal static readonly BigInteger P256Order =
            BigInteger.Parse("0FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551", System.Globalization.NumberStyles.HexNumber);
        internal static readonly BigInteger P256HalfOrder = P256Order / 2;

        private const int RpIdHashLength = 32;
        private const int FlagsOffset = RpIdHashLength;
        private const int SignCountLength = 4;
        private const int AaguidLength = 16;
        private const int AttestedCredentialDataOffset = RpIdHashLength + 1 + SignCountLength;
        private const byte AttestedCredentialDataFlag = 0x40;

        public static byte[] NormalizeLowS(byte[] s)
        {
            var sValue = new BigInteger(s, isUnsigned: true, isBigEndian: true);
            if (sValue <= P256HalfOrder)
            {
                return s;
            }

            return ToFixed32(P256Order - sValue);
        }

        public static (byte[] r, byte[] s) DecodeDerEcdsaSignatureToLowS(byte[] der)
        {
            var reader = new AsnReader(der, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            var r = sequence.ReadInteger();
            var s = sequence.ReadInteger();
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();

            return (ToFixed32(r), NormalizeLowS(ToFixed32(s)));
        }

        public static (BigInteger x, BigInteger y) DecodeP256PublicKeyFromSpki(byte[] spki)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            var parameters = ecdsa.ExportParameters(false);
            return (
                new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true),
                new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true));
        }

        public static (BigInteger x, BigInteger y) DecodeP256PublicKeyFromCose(byte[] coseKey)
        {
            var reader = new CborReader(coseKey, CborConformanceMode.Lax);
            reader.ReadStartMap();

            int? kty = null;
            int? crv = null;
            byte[]? x = null;
            byte[]? y = null;

            while (reader.PeekState() != CborReaderState.EndMap)
            {
                var label = reader.ReadInt32();
                switch (label)
                {
                    case 1: kty = reader.ReadInt32(); break;
                    case -1: crv = reader.ReadInt32(); break;
                    case -2: x = reader.ReadByteString(); break;
                    case -3: y = reader.ReadByteString(); break;
                    default: reader.SkipValue(); break;
                }
            }
            reader.ReadEndMap();

            if (kty != 2)
            {
                throw new CryptographicException($"Unsupported COSE key type {kty?.ToString() ?? "(missing)"}; expected EC2 (2).");
            }
            if (crv != 1)
            {
                throw new CryptographicException($"Unsupported COSE curve {crv?.ToString() ?? "(missing)"}; expected P-256 (1).");
            }
            if (x == null || y == null)
            {
                throw new CryptographicException("COSE EC2 key is missing its x or y coordinate.");
            }

            return (
                new BigInteger(x, isUnsigned: true, isBigEndian: true),
                new BigInteger(y, isUnsigned: true, isBigEndian: true));
        }

        public static byte[] ExtractCosePublicKeyFromAuthenticatorData(byte[] authenticatorData)
        {
            return SplitAttestedCredentialData(authenticatorData).cosePublicKey;
        }

        public static byte[] ExtractCredentialId(byte[] authenticatorData)
        {
            return SplitAttestedCredentialData(authenticatorData).credentialId;
        }

        private static (byte[] credentialId, byte[] cosePublicKey) SplitAttestedCredentialData(byte[] authenticatorData)
        {
            if (authenticatorData == null || authenticatorData.Length < AttestedCredentialDataOffset + AaguidLength + 2)
            {
                throw new ArgumentException("authenticatorData is too short to contain attested credential data.", nameof(authenticatorData));
            }

            var flags = authenticatorData[FlagsOffset];
            if ((flags & AttestedCredentialDataFlag) == 0)
            {
                throw new ArgumentException("authenticatorData does not have the attested-credential-data (AT) flag set.", nameof(authenticatorData));
            }

            var credentialIdLengthOffset = AttestedCredentialDataOffset + AaguidLength;
            var credentialIdLength = (authenticatorData[credentialIdLengthOffset] << 8) | authenticatorData[credentialIdLengthOffset + 1];

            var credentialIdOffset = credentialIdLengthOffset + 2;
            var cosePublicKeyOffset = credentialIdOffset + credentialIdLength;

            if (authenticatorData.Length <= cosePublicKeyOffset)
            {
                throw new ArgumentException("authenticatorData is shorter than its declared credentialIdLength, or has no COSE key after it.", nameof(authenticatorData));
            }

            var credentialId = new byte[credentialIdLength];
            Buffer.BlockCopy(authenticatorData, credentialIdOffset, credentialId, 0, credentialIdLength);

            var cosePublicKeyLength = authenticatorData.Length - cosePublicKeyOffset;
            var cosePublicKey = new byte[cosePublicKeyLength];
            Buffer.BlockCopy(authenticatorData, cosePublicKeyOffset, cosePublicKey, 0, cosePublicKeyLength);

            return (credentialId, cosePublicKey);
        }

        public static byte[] ExtractAuthDataFromAttestationObject(byte[] attestationObject)
        {
            var reader = new CborReader(attestationObject, CborConformanceMode.Lax);
            reader.ReadStartMap();

            byte[]? authData = null;
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                var key = reader.ReadTextString();
                if (key == "authData")
                {
                    authData = reader.ReadByteString();
                }
                else
                {
                    reader.SkipValue();
                }
            }
            reader.ReadEndMap();

            return authData ?? throw new InvalidOperationException("attestationObject has no authData field.");
        }

        private static byte[] ToFixed32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32)
            {
                return bytes;
            }
            if (bytes.Length > 32)
            {
                throw new CryptographicException("Value does not fit in a 32-byte P-256 field element.");
            }

            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }
    }
}
