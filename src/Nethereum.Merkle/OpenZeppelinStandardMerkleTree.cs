using Nethereum.ABI;
using Nethereum.Util;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using System.Collections.Generic;


namespace Nethereum.Merkle
{
    /// <summary>
    /// Implmentation of OpenZeppelin's StandardMerkleTree, same as https://github.com/OpenZeppelin/merkle-tree/blob/master/src/standard.ts
    /// This can be used to create same proof and tree as OpenZeppelin's JavaScript library and can be used when verifying against their 
    /// standard implementation https://github.com/binodnp/openzeppelin-solidity/blob/master/contracts/cryptography/MerkleProof.sol 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OpenZeppelinStandardMerkleTree<T> : MerkleTree<T>
    {
        public OpenZeppelinStandardMerkleTree() : base(new Sha3KeccackHashProvider(), new OpenZeppelinByteConverter<T>(), PairingConcatType.Sorted)
        { }
        protected override void InitialiseLeavesAndLayersAndBuildTree(List<MerkleTreeNode> leaves)
        {
            leaves.Sort(new MerkleTreeNodeComparer());
            base.InitialiseLeavesAndLayersAndBuildTree(leaves);
        }
        
        private class OpenZeppelinByteConverter<T> : IByteArrayConvertor<T>
        {
            private readonly ABIEncode _abiEncode;
            public OpenZeppelinByteConverter()
            {
                _abiEncode = new ABIEncode();
            }
            public byte[] ConvertToByteArray(T data)
            {
                var encoded = _abiEncode.GetABIParamsEncoded(data);
                return Sha3Keccack.Current.CalculateHash(encoded);

            }
        }
    }
}
