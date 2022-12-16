using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Merkle
{
    public class MerkleTreeNodeComparer : IComparer<MerkleTreeNode>
    {
        public static readonly MerkleTreeNodeComparer Current = new MerkleTreeNodeComparer();
        
        private ByteListComparer byteListComparer;
        
        public MerkleTreeNodeComparer()
        {
            byteListComparer = new ByteListComparer();
        }
        public int Compare(MerkleTreeNode x, MerkleTreeNode y)
        {
            return byteListComparer.Compare(x.Hash.ToList(), y.Hash.ToList());
        }
    }

}
