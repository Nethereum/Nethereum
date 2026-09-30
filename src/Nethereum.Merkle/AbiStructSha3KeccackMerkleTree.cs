using Nethereum.ABI.ByteArrayConvertors;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.HashProviders;
using System.Collections.Generic;


namespace Nethereum.Merkle
{

    public class AbiStructSha3KeccackMerkleTree<T> : MerkleTree<T>
    {
        public AbiStructSha3KeccackMerkleTree() : base(Sha3KeccackHashProvider.Instance, new AbiStructSha3KeccackHashByteArrayConvertor<T>(), PairingConcatType.Sorted)
        { }

        protected override void InitialiseLeavesAndLayersAndBuildTree(List<MerkleTreeNode> leaves)
        {
            leaves.Sort(new MerkleTreeNodeComparer());
            base.InitialiseLeavesAndLayersAndBuildTree(leaves);
        }
    }
}
