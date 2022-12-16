using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Merkle
{
    public class MerkleTreeNodeComparer : IComparer<MerkleTreeNode>
    {
        public static readonly MerkleTreeNodeComparer Current = new MerkleTreeNodeComparer();
        
        private ByteArrayComparer byteComparer;
        
        public MerkleTreeNodeComparer()
        {
            byteComparer = new ByteArrayComparer();
        }
        public int Compare(MerkleTreeNode x, MerkleTreeNode y)
        {
            return byteComparer.Compare(x.Hash, y.Hash);
        }
    }

}
