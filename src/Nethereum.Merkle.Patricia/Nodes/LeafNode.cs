using Nethereum.Util.HashProviders;
using System.Collections.Generic;



using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.Nodes
{
    public class LeafNode:Node
    {
        private byte[] _nibbles;
        private byte[] _value;

        public LeafNode() : this(Sha3KeccackHashProvider.Instance)
        {

        }
        public LeafNode(IHashProvider hashProvider) : base(hashProvider)
        {

        }

        public byte[] Nibbles
        {
            get => _nibbles;
            set
            {
                _nibbles = value;
                MarkDirty();
            }
        }

        public byte[] Value
        {
            get => _value;
            set
            {
                _value = value;
                MarkDirty();
            }
        }

        protected override byte[] EncodeCore() => LeafNodeRlpSerializer.Encode(this);

        public byte[] GetPrefixedNibbles()
        {
            var returnArray = new List<byte>();
            if (Nibbles.Length % 2 > 0)
            {
                returnArray.Add(3);
               
            }
            else
            {
                returnArray.Add(2);
                returnArray.Add(0);
            }

            returnArray.AddRange(Nibbles);
            return returnArray.ToArray();
        }
    }
}
