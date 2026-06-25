using System.Net;
using System.Text;
using Nethereum.Model.Enr;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Enr
{
    public class EnrRecordTests
    {
        [Fact]
        public void SignAndVerify_RoundTrip()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 1 };
            record.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
            record.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            record.Pairs["tcp"] = new byte[] { 0x76, 0x5f };

            EnrRecordSigner.Sign(record, key);

            Assert.Equal(64, record.Signature.Length);
            Assert.Equal("v4", record.Id);
            Assert.NotNull(record.Secp256k1);
            Assert.Equal(33, record.Secp256k1.Length);

            Assert.True(EnrRecordSigner.Verify(record));
        }

        [Fact]
        public void Encode_Decode_PreservesAllFields()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 42 };
            record.Pairs["ip"] = new byte[] { 192, 168, 1, 100 };
            record.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            record.Pairs["tcp"] = new byte[] { 0x76, 0x60 };

            EnrRecordSigner.Sign(record, key);
            var encoded = EnrRecordEncoder.EncodeRecord(record);
            var decoded = EnrRecordEncoder.Decode(encoded);

            Assert.Equal(record.Sequence, decoded.Sequence);
            Assert.Equal(record.Signature.ToHex(), decoded.Signature.ToHex());
            Assert.Equal(record.Pairs["ip"].ToHex(), decoded.Pairs["ip"].ToHex());
            Assert.Equal(record.Pairs["tcp"].ToHex(), decoded.Pairs["tcp"].ToHex());
            Assert.Equal(record.Pairs["udp"].ToHex(), decoded.Pairs["udp"].ToHex());
            Assert.Equal(record.Secp256k1.ToHex(), decoded.Secp256k1.ToHex());

            Assert.True(EnrRecordSigner.Verify(decoded));
        }

        [Fact]
        public void Tampered_Sequence_FailsVerification()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 1 };
            EnrRecordSigner.Sign(record, key);

            record.Sequence = 999;
            Assert.False(EnrRecordSigner.Verify(record));
        }

        [Fact]
        public void TextUrl_RoundTrip()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 5 };
            record.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
            record.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            EnrRecordSigner.Sign(record, key);

            var url = EnrRecordEncoder.ToUrl(record);
            Assert.StartsWith("enr:", url);

            var decoded = EnrRecordEncoder.ParseUrl(url);
            Assert.Equal(record.Sequence, decoded.Sequence);
            Assert.True(EnrRecordSigner.Verify(decoded));
        }

        [Fact]
        public void RecordWithoutEndpoint_IsStillValid()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 1 };
            EnrRecordSigner.Sign(record, key);

            Assert.True(EnrRecordSigner.Verify(record));
            Assert.Null(record.IP4);
            Assert.Null(record.UdpPort);
            Assert.Equal("v4", record.Id);
        }

        [Fact]
        public void IP4_And_PortAccessors_ReturnTypedValues()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 1 };
            record.Pairs["ip"] = new byte[] { 10, 0, 0, 1 };
            record.Pairs["udp"] = new byte[] { 0x30, 0x39 };
            EnrRecordSigner.Sign(record, key);

            Assert.Equal(IPAddress.Parse("10.0.0.1"), record.IP4);
            Assert.Equal((ushort)12345, record.UdpPort);
        }
    }
}
