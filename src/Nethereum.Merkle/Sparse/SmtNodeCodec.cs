using System;

namespace Nethereum.Merkle.Sparse
{
    public static class SmtNodeCodec
    {
        private const byte TypeLeaf = 0x00;
        private const byte TypeBranch = 0x01;

        public static byte[] EncodeLeaf(byte[] path, byte[] valueBytes)
        {
            var result = new byte[1 + 2 + path.Length + 2 + valueBytes.Length];
            int offset = 0;

            result[offset++] = TypeLeaf;

            result[offset++] = (byte)(path.Length >> 8);
            result[offset++] = (byte)path.Length;
            Array.Copy(path, 0, result, offset, path.Length);
            offset += path.Length;

            result[offset++] = (byte)(valueBytes.Length >> 8);
            result[offset++] = (byte)valueBytes.Length;
            Array.Copy(valueBytes, 0, result, offset, valueBytes.Length);

            return result;
        }

        public static byte[] EncodeBranch(byte[] leftHash, byte[] rightHash)
        {
            var result = new byte[1 + leftHash.Length + rightHash.Length];
            result[0] = TypeBranch;
            Array.Copy(leftHash, 0, result, 1, leftHash.Length);
            Array.Copy(rightHash, 0, result, 1 + leftHash.Length, rightHash.Length);
            return result;
        }

        public static bool IsBranch(byte[] data) => data != null && data.Length > 0 && data[0] == TypeBranch;
        public static bool IsLeaf(byte[] data) => data != null && data.Length > 0 && data[0] == TypeLeaf;

        public static void DecodeBranch(byte[] data, int hashSize, out byte[] leftHash, out byte[] rightHash)
        {
            leftHash = new byte[hashSize];
            rightHash = new byte[hashSize];
            Array.Copy(data, 1, leftHash, 0, hashSize);
            Array.Copy(data, 1 + hashSize, rightHash, 0, hashSize);
        }

        public static void DecodeLeaf(byte[] data, out byte[] path, out byte[] valueBytes)
        {
            int offset = 1;

            int pathLen = (data[offset] << 8) | data[offset + 1];
            offset += 2;
            path = new byte[pathLen];
            Array.Copy(data, offset, path, 0, pathLen);
            offset += pathLen;

            int valLen = (data[offset] << 8) | data[offset + 1];
            offset += 2;
            valueBytes = new byte[valLen];
            Array.Copy(data, offset, valueBytes, 0, valLen);
        }
    }
}
