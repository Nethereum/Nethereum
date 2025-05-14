using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle
{

public class MerkleTree<T>
    {
        private readonly IHashProvider _hashProvider;
        private readonly IByteArrayConvertor<T> _byteArrayConvertor;
        private readonly PairingConcatType _pairingConcatType;
        private MerkleTreeNode _root;
        public List<MerkleTreeNode> Leaves { get; protected set; }
        public List<List<MerkleTreeNode>> Layers { get; protected set; }

        public MerkleTree(IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, PairingConcatType pairingConcatType = PairingConcatType.Sorted)
        {
            _hashProvider = hashProvider;
            _byteArrayConvertor = byteArrayConvertor;
            _pairingConcatType = pairingConcatType;
        }

        public MerkleTreeNode Root
        {
            get { return _root; }
        }

        public void BuildTree(List<T> items)
        {
            var nodes = new List<MerkleTreeNode>();
            foreach (var item in items)
            {
                var merkleTreeNode = CreateMerkleTreeNode(item);
                nodes.Add(merkleTreeNode);
            }

            InitialiseLeavesAndLayersAndBuildTree(nodes);
        }

        
        protected virtual void InitialiseLeavesAndLayersAndBuildTree(List<MerkleTreeNode> leaves)
        {
            //This can be overriden to sort the leaves, add additonal leaves or add a strategy
            Leaves = leaves;
            Layers = new List<List<MerkleTreeNode>> { Leaves }; 
            _root = BuildTree(Leaves);
        }

        public MerkleTreeNode BuildTree(List<MerkleTreeNode> nodes)
        {
            while (nodes.Count > 1)
            {
                var layerIndex = Layers.Count;
                Layers.Insert(layerIndex, new List<MerkleTreeNode>());
                for (var i = 0; i < nodes.Count; i += 2)
                {
                    if (i + 1 == nodes.Count)
                    {
                        if (nodes.Count % 2 == 1)
                        {
                            Layers[layerIndex].Add(nodes[i].Clone());
                            continue;
                        }
                    }
                    
                    var left = nodes[i];
                    var right = i + 1 == nodes.Count ? left : nodes[i + 1];
                    byte[] hash = ConcatAndHashPair(left, right);

                    Layers[layerIndex].Add(new MerkleTreeNode(hash));
                }
                nodes = Layers[layerIndex];
            }
            return nodes[0];
        }

        public virtual void InsertLeaf(T item)
        {
            var merkleTreeNode = CreateMerkleTreeNode(item);
            Leaves.Add(merkleTreeNode);
            InitialiseLeavesAndLayersAndBuildTree(Leaves);
        }

        public virtual void InsertLeaves(IEnumerable<T> items)
        {
            var nodes = new List<MerkleTreeNode>();
            foreach (var item in items)
            {
                var merkleTreeNode = CreateMerkleTreeNode(item);
                nodes.Add(merkleTreeNode);
            }

            Leaves.AddRange(nodes);
            InitialiseLeavesAndLayersAndBuildTree(Leaves);
        }

        protected virtual MerkleTreeNode CreateMerkleTreeNode(T item)
        {
            var hash = _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(item));
            return new MerkleTreeNode(hash);
        }

        private byte[] ConcatAndHashPair(MerkleTreeNode left, MerkleTreeNode right)
        {
            return ConcatAndHashPair(left.Hash, right.Hash, _hashProvider, _pairingConcatType);
        }
        
        public static byte[] ConcatAndHashPair(byte[] left, byte[] right, IHashProvider hashProvider, PairingConcatType pairingConcatType = PairingConcatType.Sorted)
        {
            var concat = PairingConcatFactory.GetPairConcatStrategy(pairingConcatType).Concat(left, right);
            var hash = hashProvider.ComputeHash(concat);
            return hash;
        }

        public bool VerifyProof(IEnumerable<byte[]> proof, byte[] itemHash)
        {
            return VerifyProof(proof, Root.Hash, itemHash, _hashProvider);
        }

        public bool VerifyProof(IEnumerable<byte[]> proof, T item)
        {
            return VerifyProof(proof, Root.Hash, _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(item)), _hashProvider, _pairingConcatType);
        }

        public static bool VerifyProof(IEnumerable<byte[]> proof, byte[] rootHash, byte[] itemHash, IHashProvider hashProvider, PairingConcatType pairingConcatType = PairingConcatType.Sorted)
        {
            var hash = itemHash;
            foreach (var proofHash in proof)
            {
                hash = ConcatAndHashPair(proofHash, hash, hashProvider, pairingConcatType);
            }
            return hash.SequenceEqual(rootHash);
        }

        public List<byte[]> GetProof(T item)
        {
            return GetProof(_hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(item)));
        }

        public List<byte[]> GetProof(byte[] hashLeaf)
        {
            var index = -1;
            for (var i = 0; i < Leaves.Count; i++)
            {
                if (Leaves[i].Matches(hashLeaf))
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                throw new Exception("Leaf not found");
            }
            return GetProof(index);
        }

        public List<byte[]> GetProof(int index)
        {
            var proofs = new List<byte[]>();
            for (var i = 0; i < Layers.Count; i++)
            {
                var isRightNode = index % 2 == 1;
                var pairIndex = isRightNode ? index - 1 : index + 1;
                var currentLayer = Layers[i];
                if (pairIndex < currentLayer.Count)
                {
                    proofs.Add(currentLayer[pairIndex].Hash);
                }   
                index = (index / 2) | 0;
            }
            return proofs;
        }

    }

}
