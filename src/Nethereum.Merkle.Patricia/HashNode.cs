using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using System;
using System.Diagnostics;

namespace Nethereum.Merkle.Patricia
{
    public class HashNode : Node
    {
        private Node _innerNode;

        public HashNode() : this(new Sha3KeccackHashProvider())
        {

        }

        public HashNode(IHashProvider hashProvider) : base(hashProvider)
        {

        }

        public byte[] Hash { get; set; }

        public Node InnerNode
        {
            get => _innerNode;
            set
            {
                _innerNode = value;
                MarkDirty();
            }
        }

        public override byte[] GetHash()
        {
            if (_innerNode == null)
                return Hash;

            if (_innerNode is EmptyNode)
                return Hash;

            if (_innerNode.IsDirty)
            {
                var newHash = _innerNode.GetHash();
                Hash = newHash;
                return newHash;
            }

            return _innerNode.GetHash();
        }

        public void DecodeInnerNode(ITrieStorage storage, bool decodeInnerHashNodes)
        {
           var node = new NodeDecoder().DecodeNode(Hash, decodeInnerHashNodes, storage);
           _innerNode = node;
        }

        public override byte[] GetRLPEncodedDataCore()
        {
            if (InnerNode is EmptyNode) return Hash; // if we have an empty node this might be due to partial storage so we return the original hash
            if (InnerNode != null) return InnerNode.GetRLPEncodedData();
            //the hash is 32 bytes so it will use the hash of the node regardless
            return Hash;

        }
    }
}
