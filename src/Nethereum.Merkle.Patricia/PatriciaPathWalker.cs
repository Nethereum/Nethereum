using System;

namespace Nethereum.Merkle.Patricia
{
    /// <summary>
    /// Walks a Patricia trie following an explicit nibble path from the root,
    /// returning the RLP-encoded bytes of the node at the path's endpoint.
    ///
    /// Used by the snap/1 GetTrieNodes handler: each requested path is a
    /// compact-hex-encoded prefix of the canonical state trie's nibble
    /// trajectory, and the server's reply is the trie-node RLP at that point.
    /// </summary>
    public static class PatriciaPathWalker
    {
        /// <summary>
        /// Decode the Ethereum "compact hex" path encoding used by snap/1
        /// GetTrieNodes paths and by hex-prefix-encoded extension/leaf nodes:
        ///   byte[0] high nibble = flags (low bit = odd length, bit 1 = terminator)
        ///   byte[0] low nibble  = first nibble (if odd) else padding (must be 0)
        ///   byte[i>=1]          = two nibbles each
        /// Snap paths drop the terminator flag, so flags here are 0 or 1.
        /// </summary>
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

        /// <summary>
        /// Inverse of <see cref="CompactToNibbles"/>: hex-prefix (compact)
        /// encoding without the terminator flag — the snap/1 GetTrieNodes path
        /// shape. Even paths: byte[0]=0x00. Odd paths: byte[0]=0x10|nibble0.
        /// </summary>
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

        /// <summary>
        /// Walk the trie from the given root following pathNibbles, returning
        /// the RLP-encoded bytes of the trie node at the path endpoint, or
        /// <c>new byte[0]</c> if the path diverges from the trie (e.g., the
        /// path is too long, or runs into a branch/leaf prefix that doesn't
        /// match). The empty-bytes return matches the snap/1 GetTrieNodes
        /// semantics so the receiver hashes them
        /// to <c>keccak256("")</c>.
        /// </summary>
        public static byte[] WalkPath(Node root, ITrieStorage storage, byte[] pathNibbles)
        {
            if (root == null || root is EmptyNode) return new byte[0];
            return Walk(root, storage, pathNibbles, 0);
        }

        private static byte[] Walk(Node node, ITrieStorage storage, byte[] path, int offset)
        {
            if (node is HashNode hash)
            {
                if (hash.InnerNode == null && storage != null)
                    hash.DecodeInnerNode(storage, false);
                if (hash.InnerNode == null) return new byte[0];
                node = hash.InnerNode;
            }
            if (node == null || node is EmptyNode) return new byte[0];

            if (offset >= path.Length)
            {
                // Reached the path endpoint — return this node's RLP bytes.
                return node.GetRLPEncodedData();
            }

            switch (node)
            {
                case LeafNode:
                    // Path expects more nibbles but we're at a terminal leaf.
                    return new byte[0];

                case BranchNode branch:
                {
                    var child = branch.Children[path[offset]];
                    if (child == null || child is EmptyNode) return new byte[0];
                    return Walk(child, storage, path, offset + 1);
                }

                case ExtendedNode ext:
                {
                    var extNibbles = ext.Nibbles;
                    if (path.Length - offset < extNibbles.Length) return new byte[0];
                    for (int i = 0; i < extNibbles.Length; i++)
                        if (path[offset + i] != extNibbles[i]) return new byte[0];
                    return Walk(ext.InnerNode, storage, path, offset + extNibbles.Length);
                }
            }
            return new byte[0];
        }
    }
}
