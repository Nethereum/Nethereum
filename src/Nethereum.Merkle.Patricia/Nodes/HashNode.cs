using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using System;
using System.Diagnostics;

using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.Nodes
{
    public class HashNode : Node
    {
        private Node _innerNode;

        public HashNode() : this(Sha3KeccackHashProvider.Instance)
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

        public void ReleaseInnerNode() => _innerNode = null;

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

        public void DecodeInnerNode(ITrieNodeStore store, bool decodeInnerHashNodes)
        {
            _innerNode = new NodeDecoder().Decode(this, store, decodeInnerHashNodes);
        }

        protected override byte[] EncodeCore() => HashNodeRlpSerializer.Encode(this);
    }
}
