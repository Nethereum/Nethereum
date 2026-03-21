using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Merkle;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;

namespace Nethereum.PrivacyPools
{
    public class PoseidonMerkleTree
    {
        private readonly LeanIncrementalMerkleTree<byte[]> _tree;
        private readonly PoseidonHasher _hasher;

        public PoseidonMerkleTree()
        {
            _hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            var hashProvider = new PoseidonPairHashProvider(_hasher);
            var convertor = new ByteArrayToByteArrayConvertor();
            _tree = new LeanIncrementalMerkleTree<byte[]>(hashProvider, convertor, PairingConcatType.Normal, hashLeafOnInsert: false);
        }

        public byte[] Root => _tree.Root;

        public int Size => _tree.Size;

        public int Depth => _tree.Depth;

        public BigInteger RootAsBigInteger
        {
            get
            {
                if (_tree.Root == null || _tree.Root.Length == 0)
                    return BigInteger.Zero;
                return new BigInteger(_tree.Root, isUnsigned: true, isBigEndian: true);
            }
        }

        public void InsertCommitment(BigInteger commitmentHash)
        {
            var bytes = BigIntegerToBytes32(commitmentHash);
            _tree.InsertLeaf(bytes);
        }

        public void InsertCommitments(IEnumerable<BigInteger> commitmentHashes)
        {
            var bytesList = new List<byte[]>();
            foreach (var hash in commitmentHashes)
            {
                bytesList.Add(BigIntegerToBytes32(hash));
            }
            _tree.InsertMany(bytesList);
        }

        public MerkleProof GenerateInclusionProof(int leafIndex)
        {
            return _tree.GenerateProof(leafIndex);
        }

        public bool VerifyInclusionProof(MerkleProof proof, BigInteger commitmentHash)
        {
            var bytes = BigIntegerToBytes32(commitmentHash);
            return _tree.VerifyProof(proof, bytes, _tree.Root);
        }

        public bool VerifyInclusionProof(MerkleProof proof, BigInteger commitmentHash, byte[] root)
        {
            var bytes = BigIntegerToBytes32(commitmentHash);
            return _tree.VerifyProof(proof, bytes, root);
        }

        public BigInteger[] GetProofSiblings(MerkleProof proof)
        {
            var siblings = new BigInteger[proof.ProofNodes.Count];
            for (int i = 0; i < proof.ProofNodes.Count; i++)
            {
                siblings[i] = new BigInteger(proof.ProofNodes[i], isUnsigned: true, isBigEndian: true);
            }
            return siblings;
        }

        public int[] GetProofPathIndices(MerkleProof proof)
        {
            return proof.PathIndices.ToArray();
        }

        public string Export()
        {
            return _tree.Export(bytes => Convert.ToBase64String(bytes));
        }

        public static PoseidonMerkleTree Import(string json, ILeanIMTNodeStorage storage = null, bool verifyIntegrity = false)
        {
            var tree = new PoseidonMerkleTree();
            var imported = LeanIncrementalMerkleTree<byte[]>.Import(
                new PoseidonPairHashProvider(new PoseidonHasher(PoseidonParameterPreset.CircomT2)),
                new ByteArrayToByteArrayConvertor(),
                json,
                str => Convert.FromBase64String(str),
                nodeParser: null,
                pairingConcatType: PairingConcatType.Normal,
                hashLeafOnInsert: false,
                storage: storage,
                verifyIntegrity: verifyIntegrity);

            return new PoseidonMerkleTree(imported);
        }

        private PoseidonMerkleTree(LeanIncrementalMerkleTree<byte[]> imported)
        {
            _hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            _tree = imported;
        }

        private static byte[] BigIntegerToBytes32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32)
                return bytes;

            var padded = new byte[32];
            if (bytes.Length > 32)
            {
                Array.Copy(bytes, bytes.Length - 32, padded, 0, 32);
            }
            else
            {
                Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            }
            return padded;
        }
    }
}
