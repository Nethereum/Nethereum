using Nethereum.Util.HashProviders;
using System;

namespace Nethereum.Merkle.Patricia
{
    public class HashNode : Node
    {
        public HashNode() : this(new Sha3KeccackHashProvider())
        {

        }

        public HashNode(IHashProvider hashProvider) : base(hashProvider)
        {
 
        }

        public byte[] Hash { get; set; }
        public Node InnerNode { get; set; }

        public override byte[] GetHash()
        {
            if (InnerNode == null)
                return Hash;
            var innerNodeHash = InnerNode.GetHash();
            if (innerNodeHash.AreTheSame(Hash))
            {
                return Hash;
            }
            else
            {
                throw new Exception("Hash node inner node hash does not match current hash");
            }
        }

        public void DecodeInnerNode(ITrieStorage storage, bool decodeInnerHashNodes)
        {
            var node =  new NodeDecoder().DecodeNode(Hash, decodeInnerHashNodes, storage);
            InnerNode = node;
        }

        public override byte[] GetRLPEncodedData()
        {
            if (InnerNode != null) return InnerNode.GetRLPEncodedData();
            //the hash is 32 bytes so it will use the hash of the node regardless
            return Hash;

        }
    }
}
