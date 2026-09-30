using System;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.Proofs
{
    public static class PatriciaPathWalker
    {
        public static byte[] CompactToNibbles(byte[] compact)
        {
            if (compact == null || compact.Length == 0) return new byte[0];
            var flag = (compact[0] >> 4) & 0x0f;
            var isOdd = (flag & 1) != 0;

            int total = (compact.Length - 1) * 2 + (isOdd ? 1 : 0);
            var nibbles = new byte[total];
            int o = 0;
            if (isOdd) nibbles[o++] = (byte)(compact[0] & 0x0f);
            for (int i = 1; i < compact.Length; i++)
            {
                nibbles[o++] = (byte)((compact[i] >> 4) & 0x0f);
                nibbles[o++] = (byte)(compact[i] & 0x0f);
            }
            return nibbles;
        }

        public static byte[] NibblesToCompact(byte[] nibbles)
        {
            if (nibbles == null || nibbles.Length == 0) return new byte[] { 0x00 };
            bool isOdd = (nibbles.Length & 1) != 0;
            int outLen = 1 + (isOdd ? (nibbles.Length - 1) / 2 : nibbles.Length / 2);
            var compact = new byte[outLen];
            int srcStart;
            if (isOdd)
            {
                compact[0] = (byte)(0x10 | (nibbles[0] & 0x0f));
                srcStart = 1;
            }
            else
            {
                compact[0] = 0x00;
                srcStart = 0;
            }
            int dst = 1;
            for (int i = srcStart; i < nibbles.Length; i += 2)
                compact[dst++] = (byte)((nibbles[i] << 4) | (nibbles[i + 1] & 0x0f));
            return compact;
        }

        public static byte[] WalkPath(Node root, ITrieNodeStore store, byte[] pathNibbles)
        {
            if (root == null || root is EmptyNode) return new byte[0];
            return Walk(root, store, pathNibbles, 0);
        }

        private static byte[] Walk(Node node, ITrieNodeStore store, byte[] path, int offset)
        {
            if (node is HashNode hash)
            {
                if (hash.InnerNode == null && store != null)
                    hash.DecodeInnerNode(store, false);
                if (hash.InnerNode == null) return new byte[0];
                node = hash.InnerNode;
            }
            if (node == null || node is EmptyNode) return new byte[0];

            if (offset >= path.Length)
            {
                return node.GetEncodedData();
            }

            switch (node)
            {
                case LeafNode:
                    return new byte[0];

                case BranchNode branch:
                {
                    var child = branch.Children[path[offset]];
                    if (child == null || child is EmptyNode) return new byte[0];
                    return Walk(child, store, path, offset + 1);
                }

                case ExtendedNode ext:
                {
                    var extNibbles = ext.Nibbles;
                    if (path.Length - offset < extNibbles.Length) return new byte[0];
                    for (int i = 0; i < extNibbles.Length; i++)
                        if (path[offset + i] != extNibbles[i]) return new byte[0];
                    return Walk(ext.InnerNode, store, path, offset + extNibbles.Length);
                }
            }
            return new byte[0];
        }
    }
}
