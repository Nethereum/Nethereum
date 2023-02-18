using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using System;
using System.Diagnostics;

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
                if(InnerNode is EmptyNode)
                {
                    return Hash; // if we have an empty node this might be due to partial storage so we return the original hash
                }
                throw new Exception("Hash node inner node hash does not match current hash");
            }

        }

        public void DecodeInnerNode(ITrieStorage storage, bool decodeInnerHashNodes)
        {
           var node = new NodeDecoder().DecodeNode(Hash, decodeInnerHashNodes, storage);
           InnerNode = node;
            
        }

        public override byte[] GetRLPEncodedData()
        {
            if (InnerNode is EmptyNode) return Hash; // if we have an empty node this might be due to partial storage so we return the original hash
            if (InnerNode != null) return InnerNode.GetRLPEncodedData();
            //the hash is 32 bytes so it will use the hash of the node regardless
            return Hash;

        }
    }
}
