using System;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Merkle.Patricia
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "missing-trie-node", "A hash reference with no node in the store throws")]
    public class MissingTrieNodeException : Exception
    {
        public MissingTrieNodeException(byte[] owner, byte[] path, byte[] hash)
            : base(BuildMessage(owner, path, hash))
        {
            Owner = owner ?? new byte[0];
            Path = path ?? new byte[0];
            Hash = hash;
        }

        public byte[] Owner { get; }
        public byte[] Path { get; }
        public byte[] Hash { get; }

        private static string BuildMessage(byte[] owner, byte[] path, byte[] hash)
        {
            var ownerHex = owner == null || owner.Length == 0 ? "" : " (owner " + owner.ToHex(true) + ")";
            return "missing trie node " + (hash == null ? "null" : hash.ToHex(true)) + ownerHex + " (path " + (path == null ? "0x" : path.ToHex(true)) + ")";
        }
    }
}
