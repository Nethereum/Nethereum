using System;
using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.Storage
{
    public struct CheckpointEntry
    {
        public byte[] Hash;
        public byte[] Encoded;
        public int Depth;
        public byte NodeType;
        public byte[] Stem;
    }

    public static class BinaryTrieCheckpointSerializer
    {
        public static byte[] Export(IReadOnlyList<NodeEntry> nodes)
        {
            var totalSize = 4;
            foreach (var n in nodes)
                totalSize += 4 + n.Hash.Length + 4 + n.Encoded.Length + 4 + 1 +
                             (n.Stem != null ? n.Stem.Length : 0);

            var buffer = new byte[totalSize];
            int offset = 0;

            WriteInt32BE(buffer, ref offset, nodes.Count);

            foreach (var n in nodes)
            {
                WriteInt32BE(buffer, ref offset, n.Hash.Length);
                Array.Copy(n.Hash, 0, buffer, offset, n.Hash.Length);
                offset += n.Hash.Length;

                WriteInt32BE(buffer, ref offset, n.Encoded.Length);
                Array.Copy(n.Encoded, 0, buffer, offset, n.Encoded.Length);
                offset += n.Encoded.Length;

                WriteInt32BE(buffer, ref offset, n.Depth);
                buffer[offset++] = n.NodeType;

                if (n.Stem != null)
                {
                    Array.Copy(n.Stem, 0, buffer, offset, n.Stem.Length);
                    offset += n.Stem.Length;
                }
            }

            var result = new byte[offset];
            Array.Copy(buffer, result, offset);
            return result;
        }

        public static List<CheckpointEntry> Import(byte[] checkpoint)
        {
            var entries = new List<CheckpointEntry>();
            if (checkpoint == null || checkpoint.Length < 4) return entries;

            int offset = 0;
            int count = ReadInt32BE(checkpoint, ref offset);

            for (int i = 0; i < count; i++)
            {
                int hashLen = ReadInt32BE(checkpoint, ref offset);
                var hash = new byte[hashLen];
                Array.Copy(checkpoint, offset, hash, 0, hashLen);
                offset += hashLen;

                int encodedLen = ReadInt32BE(checkpoint, ref offset);
                var encoded = new byte[encodedLen];
                Array.Copy(checkpoint, offset, encoded, 0, encodedLen);
                offset += encodedLen;

                int depth = ReadInt32BE(checkpoint, ref offset);
                byte nodeType = checkpoint[offset++];

                byte[] stem = null;
                if (nodeType == BinaryTrieConstants.NodeTypeStem)
                {
                    stem = new byte[BinaryTrieConstants.StemSize];
                    Array.Copy(checkpoint, offset, stem, 0, BinaryTrieConstants.StemSize);
                    offset += BinaryTrieConstants.StemSize;
                }

                entries.Add(new CheckpointEntry
                {
                    Hash = hash,
                    Encoded = encoded,
                    Depth = depth,
                    NodeType = nodeType,
                    Stem = stem
                });
            }

            return entries;
        }

        private static void WriteInt32BE(byte[] buffer, ref int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
            offset += 4;
        }

        private static int ReadInt32BE(byte[] buffer, ref int offset)
        {
            int value = (buffer[offset] << 24) | (buffer[offset + 1] << 16) |
                        (buffer[offset + 2] << 8) | buffer[offset + 3];
            offset += 4;
            return value;
        }
    }
}
