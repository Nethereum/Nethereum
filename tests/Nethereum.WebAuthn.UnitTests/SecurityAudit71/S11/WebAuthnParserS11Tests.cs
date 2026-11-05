using System.Formats.Asn1;
using System.Formats.Cbor;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests.SecurityAudit71.S11
{
    public class WebAuthnParserS11Tests
    {
        private static readonly BigInteger P256Order =
            BigInteger.Parse("0FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551", System.Globalization.NumberStyles.HexNumber);

        private static byte[] Cose(byte[] x, byte[] y, int kty = 2, int crv = 1)
        {
            var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
            writer.WriteStartMap(5);
            writer.WriteInt32(1); writer.WriteInt32(kty);
            writer.WriteInt32(3); writer.WriteInt32(-7);
            writer.WriteInt32(-1); writer.WriteInt32(crv);
            writer.WriteInt32(-2); writer.WriteByteString(x);
            writer.WriteInt32(-3); writer.WriteByteString(y);
            writer.WriteEndMap();
            return writer.Encode();
        }

        [Fact]
        public void Given_AGenuineP256CoseKey_When_Decoded_Then_TheCoordinatesAreReturned()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var q = key.ExportParameters(false).Q;

            var (x, y) = WebAuthnResponseParser.DecodeP256PublicKeyFromCose(Cose(q.X!, q.Y!));

            Assert.Equal(new BigInteger(q.X!, isUnsigned: true, isBigEndian: true), x);
            Assert.Equal(new BigInteger(q.Y!, isUnsigned: true, isBigEndian: true), y);
        }

        [Fact]
        public void Given_ACoseKeyWhosePointIsNotOnTheP256Curve_When_Decoded_Then_ItIsRejected()
        {
            var x = new byte[32]; x[31] = 1;
            var y = new byte[32]; y[31] = 1;

            Assert.ThrowsAny<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(Cose(x, y)));
        }

        [Fact]
        public void Given_ACoseKeyAtTheZeroPoint_When_Decoded_Then_ItIsRejected()
        {
            Assert.ThrowsAny<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(Cose(new byte[32], new byte[32])));
        }

        [Fact]
        public void Given_ACoseKeyWithA33ByteCoordinate_When_Decoded_Then_ItIsRejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var q = key.ExportParameters(false).Q;
            var oversized = new byte[33];
            oversized[0] = 0x01;
            System.Buffer.BlockCopy(q.X!, 0, oversized, 1, 32);

            Assert.ThrowsAny<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(Cose(oversized, q.Y!)));
        }

        [Fact]
        public void Given_ASpkiOfAP384Key_When_DecodedAsP256_Then_ItIsRejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

            Assert.ThrowsAny<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromSpki(key.ExportSubjectPublicKeyInfo()));
        }

        [Fact]
        public void Given_ASpkiOfABrainpoolP256Key_When_DecodedAsP256_Then_ItIsRejected()
        {
            ECDsa key;
            try { key = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1); }
            catch (System.Exception) { return; }
            using (key)
            {
                Assert.ThrowsAny<CryptographicException>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromSpki(key.ExportSubjectPublicKeyInfo()));
            }
        }

        [Fact]
        public void Given_ACoseKeyWithAMillionNestedArraysInAnUnknownLabel_When_Decoded_Then_ItFailsWithoutCrashing()
        {
            var cose = new System.Collections.Generic.List<byte> { 0xA1, 0x18, 0x63 };
            for (var i = 0; i < 1_000_000; i++) cose.Add(0x81);
            cose.Add(0x00);

            Assert.ThrowsAny<System.Exception>(() => WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cose.ToArray()));
        }
    }
}
