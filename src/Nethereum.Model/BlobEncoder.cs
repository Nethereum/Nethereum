using System;
using System.Collections.Generic;

namespace Nethereum.Model
{
    public static class BlobEncoder
    {
        public const int BLOB_SIZE = 131072;
        public const int FIELD_ELEMENTS_PER_BLOB = 4096;
        public const int BYTES_PER_FIELD_ELEMENT = 32;
        public const int USABLE_BYTES_PER_FIELD_ELEMENT = 31;
        public const int USABLE_BYTES_PER_BLOB = FIELD_ELEMENTS_PER_BLOB * USABLE_BYTES_PER_FIELD_ELEMENT;
        public const int MAX_BLOBS_PER_BLOCK = 6;

        public static List<byte[]> EncodeBlobs(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data must not be empty", nameof(data));

            var blobCount = (data.Length + USABLE_BYTES_PER_BLOB - 1) / USABLE_BYTES_PER_BLOB;
            var blobs = new List<byte[]>(blobCount);
            var offset = 0;

            for (int b = 0; b < blobCount; b++)
            {
                var blob = new byte[BLOB_SIZE];
                for (int fe = 0; fe < FIELD_ELEMENTS_PER_BLOB && offset < data.Length; fe++)
                {
                    var blobOffset = fe * BYTES_PER_FIELD_ELEMENT;
                    blob[blobOffset] = 0x00;
                    var toCopy = Math.Min(USABLE_BYTES_PER_FIELD_ELEMENT, data.Length - offset);
                    Buffer.BlockCopy(data, offset, blob, blobOffset + 1, toCopy);
                    offset += toCopy;
                }
                blobs.Add(blob);
            }

            return blobs;
        }

        public static byte[] DecodeBlobs(List<byte[]> blobs)
        {
            if (blobs == null || blobs.Count == 0)
                return new byte[0];

            var result = new byte[blobs.Count * USABLE_BYTES_PER_BLOB];
            var writeOffset = 0;

            foreach (var blob in blobs)
            {
                if (blob.Length != BLOB_SIZE)
                    throw new ArgumentException($"Blob must be {BLOB_SIZE} bytes, got {blob.Length}");

                for (int fe = 0; fe < FIELD_ELEMENTS_PER_BLOB; fe++)
                {
                    var blobOffset = fe * BYTES_PER_FIELD_ELEMENT + 1;
                    Buffer.BlockCopy(blob, blobOffset, result, writeOffset, USABLE_BYTES_PER_FIELD_ELEMENT);
                    writeOffset += USABLE_BYTES_PER_FIELD_ELEMENT;
                }
            }

            var trimmed = writeOffset;
            while (trimmed > 0 && result[trimmed - 1] == 0)
                trimmed--;

            var output = new byte[trimmed];
            Buffer.BlockCopy(result, 0, output, 0, trimmed);
            return output;
        }
    }
}
