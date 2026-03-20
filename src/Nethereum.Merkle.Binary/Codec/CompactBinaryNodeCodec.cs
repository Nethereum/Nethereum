using System;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary
{
    public static class CompactBinaryNodeCodec
    {
        public static byte[] Encode(IBinaryNode node, IHashProvider hashProvider)
        {
            if (node is InternalBinaryNode internalNode)
                return EncodeInternal(internalNode, hashProvider);
            if (node is StemBinaryNode stemNode)
                return EncodeStem(stemNode);
            return Array.Empty<byte>();
        }

        private static byte[] EncodeInternal(InternalBinaryNode node, IHashProvider hashProvider)
        {
            var result = new byte[BinaryTrieConstants.NodeTypeBytes + BinaryTrieConstants.HashSize * 2];
            result[0] = BinaryTrieConstants.NodeTypeInternal;
            var leftHash = node.Left.ComputeHash(hashProvider);
            var rightHash = node.Right.ComputeHash(hashProvider);
            Array.Copy(leftHash, 0, result, 1, BinaryTrieConstants.HashSize);
            Array.Copy(rightHash, 0, result, 1 + BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);
            return result;
        }

        private static byte[] EncodeStem(StemBinaryNode node)
        {
            int valueCount = 0;
            var bitmap = new byte[BinaryTrieConstants.BitmapSize];

            for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if (node.Values[i] != null)
                {
                    bitmap[i / 8] |= (byte)(1 << (7 - (i % 8)));
                    valueCount++;
                }
            }

            int headerSize = BinaryTrieConstants.NodeTypeBytes + BinaryTrieConstants.StemSize + BinaryTrieConstants.BitmapSize;
            var result = new byte[headerSize + valueCount * BinaryTrieConstants.HashSize];
            result[0] = BinaryTrieConstants.NodeTypeStem;
            Array.Copy(node.Stem, 0, result, BinaryTrieConstants.NodeTypeBytes, BinaryTrieConstants.StemSize);
            Array.Copy(bitmap, 0, result, BinaryTrieConstants.NodeTypeBytes + BinaryTrieConstants.StemSize, BinaryTrieConstants.BitmapSize);

            int offset = headerSize;
            for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if (node.Values[i] != null)
                {
                    Array.Copy(node.Values[i], 0, result, offset, BinaryTrieConstants.HashSize);
                    offset += BinaryTrieConstants.HashSize;
                }
            }

            return result;
        }

        public static IBinaryNode Decode(byte[] data, int depth)
        {
            if (data == null || data.Length == 0)
                return EmptyBinaryNode.Instance;

            switch (data[0])
            {
                case BinaryTrieConstants.NodeTypeInternal:
                    return DecodeInternal(data, depth);
                case BinaryTrieConstants.NodeTypeStem:
                    return DecodeStem(data, depth);
                default:
                    throw new ArgumentException("Unknown node type: " + data[0]);
            }
        }

        private static IBinaryNode DecodeInternal(byte[] data, int depth)
        {
            if (data.Length != 1 + BinaryTrieConstants.HashSize * 2)
                throw new ArgumentException("Invalid internal node data length");

            var leftHash = new byte[BinaryTrieConstants.HashSize];
            var rightHash = new byte[BinaryTrieConstants.HashSize];
            Array.Copy(data, 1, leftHash, 0, BinaryTrieConstants.HashSize);
            Array.Copy(data, 1 + BinaryTrieConstants.HashSize, rightHash, 0, BinaryTrieConstants.HashSize);

            var left = new HashedBinaryNode(leftHash, depth + 1);
            var right = new HashedBinaryNode(rightHash, depth + 1);
            return new InternalBinaryNode(depth, left, right);
        }

        private static IBinaryNode DecodeStem(byte[] data, int depth)
        {
            int headerSize = BinaryTrieConstants.NodeTypeBytes + BinaryTrieConstants.StemSize + BinaryTrieConstants.BitmapSize;
            if (data.Length < headerSize)
                throw new ArgumentException("Invalid stem node data length");

            var stem = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(data, BinaryTrieConstants.NodeTypeBytes, stem, 0, BinaryTrieConstants.StemSize);

            var bitmap = new byte[BinaryTrieConstants.BitmapSize];
            Array.Copy(data, BinaryTrieConstants.NodeTypeBytes + BinaryTrieConstants.StemSize, bitmap, 0, BinaryTrieConstants.BitmapSize);

            var values = new byte[BinaryTrieConstants.StemNodeWidth][];
            int offset = headerSize;

            for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if ((bitmap[i / 8] >> (7 - (i % 8)) & 1) == 1)
                {
                    if (data.Length < offset + BinaryTrieConstants.HashSize)
                        throw new ArgumentException("Invalid stem node data: truncated values");
                    values[i] = new byte[BinaryTrieConstants.HashSize];
                    Array.Copy(data, offset, values[i], 0, BinaryTrieConstants.HashSize);
                    offset += BinaryTrieConstants.HashSize;
                }
            }

            return new StemBinaryNode(stem, values, depth);
        }
    }
}
