using System;
using Nethereum.Documentation;

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

        public static byte[] NibblesToCompact(byte[] nibbles) => NibblesToCompact(nibbles, false);

        [NethereumDocExample(DocSection.ChainInfrastructure, "compact-encoding", "Hex-prefix encode a nibble path as extension or leaf")]
        public static byte[] NibblesToCompact(byte[] nibbles, bool leaf)
        {
            var length = nibbles == null ? 0 : nibbles.Length;
            var isOdd = (length & 1) != 0;
            var compact = new byte[1 + length / 2];
            compact[0] = (byte)((leaf ? 0x20 : 0x00) | (isOdd ? 0x10 : 0x00));
            var src = 0;
            if (isOdd) compact[0] |= (byte)(nibbles[src++] & 0x0f);
            for (var dst = 1; dst < compact.Length; dst++, src += 2)
                compact[dst] = (byte)((nibbles[src] << 4) | (nibbles[src + 1] & 0x0f));
            return compact;
        }

        internal static void ResolveInner(HashNode hash, ITrieNodeStore store)
        {
            if (hash.InnerNode == null && store != null)
                hash.DecodeInnerNode(store, false);
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
                ResolveInner(hash, store);
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
