using System.Text;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class BlobEncoderTests
    {
        [Fact]
        public void ShouldRoundTripSmallData()
        {
            var data = Encoding.UTF8.GetBytes("Hello Ethereum Blobs!");
            var blobs = BlobEncoder.EncodeBlobs(data);

            Assert.Single(blobs);
            Assert.Equal(BlobEncoder.BLOB_SIZE, blobs[0].Length);

            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(data, decoded);
        }

        [Fact]
        public void ShouldRoundTripLargeData()
        {
            var data = new byte[200000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 251 + 1);

            var blobs = BlobEncoder.EncodeBlobs(data);
            Assert.Equal(2, blobs.Count);

            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(data, decoded);
        }

        [Fact]
        public void ShouldProduceBlobsWithZeroFirstBytePerFieldElement()
        {
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
                data[i] = 0xFF;

            var blobs = BlobEncoder.EncodeBlobs(data);
            var blob = blobs[0];

            for (int fe = 0; fe < BlobEncoder.FIELD_ELEMENTS_PER_BLOB; fe++)
            {
                var offset = fe * BlobEncoder.BYTES_PER_FIELD_ELEMENT;
                Assert.Equal(0x00, blob[offset]);
            }
        }

        [Fact]
        public void ShouldFillExactlyOneBlob()
        {
            var data = new byte[BlobEncoder.USABLE_BYTES_PER_BLOB];
            data[0] = 0x42;
            data[data.Length - 1] = 0x42;

            var blobs = BlobEncoder.EncodeBlobs(data);
            Assert.Single(blobs);

            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(data, decoded);
        }

        [Fact]
        public void ShouldSplitAcrossMultipleBlobs()
        {
            var data = new byte[BlobEncoder.USABLE_BYTES_PER_BLOB + 1];
            data[0] = 0x01;
            data[data.Length - 1] = 0x01;

            var blobs = BlobEncoder.EncodeBlobs(data);
            Assert.Equal(2, blobs.Count);

            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(data, decoded);
        }

        [Fact]
        public void ShouldHandleSingleByte()
        {
            var data = new byte[] { 0x42 };
            var blobs = BlobEncoder.EncodeBlobs(data);
            Assert.Single(blobs);

            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(data, decoded);
        }

        [Fact]
        public void ShouldHaveCorrectConstants()
        {
            Assert.Equal(131072, BlobEncoder.BLOB_SIZE);
            Assert.Equal(4096, BlobEncoder.FIELD_ELEMENTS_PER_BLOB);
            Assert.Equal(32, BlobEncoder.BYTES_PER_FIELD_ELEMENT);
            Assert.Equal(31, BlobEncoder.USABLE_BYTES_PER_FIELD_ELEMENT);
            Assert.Equal(4096 * 31, BlobEncoder.USABLE_BYTES_PER_BLOB);
        }
    }
}
