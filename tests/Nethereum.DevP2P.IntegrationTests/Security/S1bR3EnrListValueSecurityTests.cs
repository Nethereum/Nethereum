using System;
using System.Linq;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Trait("Category", "Security")]
    public class S1bR3EnrListValueSecurityTests
    {
        private static readonly byte[] ValidList = new byte[] { 0xc2, 0x01, 0x02 };

        private static EnrRecord NewRecord()
        {
            var record = new EnrRecord { Sequence = 1 };
            record.Pairs["ip"] = new byte[] { 8, 8, 4, 4 };
            record.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            return record;
        }

        [Fact]
        public void Given_ListValueWhoseHeaderPromisesMoreBytesThanItHolds_When_Set_Then_ItIsRejected()
        {
            var record = NewRecord();
            Assert.ThrowsAny<ArgumentException>(() => record.SetListValue("eth", new byte[] { 0xc5, 0x01 }));
        }

        [Fact]
        public void Given_ListValueFollowedByExtraBytes_When_Set_Then_ItIsRejected()
        {
            var record = NewRecord();
            Assert.ThrowsAny<ArgumentException>(() => record.SetListValue("eth", new byte[] { 0xc2, 0x01, 0x02, 0x03, 0x04 }));
        }

        [Fact]
        public void Given_ListKeyOverwrittenWithAByteValue_When_SignedAndRoundTripped_Then_TheRecordVerifies()
        {
            var key = EthECKey.GenerateKey();
            var record = NewRecord();
            record.SetListValue("eth", ValidList);
            record.Pairs["eth"] = new byte[] { 0x01, 0x02, 0x03 };
            EnrRecordSigner.Sign(record, key);

            var decoded = EnrRecordEncoder.Decode(EnrRecordEncoder.EncodeRecord(record));

            Assert.True(EnrRecordSigner.Verify(decoded));
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, decoded.Pairs["eth"]);
        }

        [Fact]
        public void Given_ValidSignedRecordFollowedByATrailingByte_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var record = NewRecord();
            record.SetListValue("eth", ValidList);
            EnrRecordSigner.Sign(record, key);
            var encoded = EnrRecordEncoder.EncodeRecord(record).Concat(new byte[] { 0x80 }).ToArray();

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        public void Given_SignedRecordWhoseListValueIsReadBack_When_ReEncoded_Then_TheBytesAreIdentical()
        {
            var key = EthECKey.GenerateKey();
            var record = NewRecord();
            record.SetListValue("eth", ValidList);
            EnrRecordSigner.Sign(record, key);
            var encoded = EnrRecordEncoder.EncodeRecord(record);

            var decoded = EnrRecordEncoder.Decode(encoded);

            Assert.Equal(encoded, EnrRecordEncoder.EncodeRecord(decoded));
            Assert.True(EnrRecordSigner.Verify(decoded));
        }
    }
}
