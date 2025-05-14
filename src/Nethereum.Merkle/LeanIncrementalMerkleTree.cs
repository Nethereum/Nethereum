#if NET5_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Nethereum.Util;

namespace Nethereum.Merkle
{
    public class LeanIncrementalMerkleTree<T>
    {
        private readonly List<T> _leaves = new List<T>();
        private readonly IHashProvider _hashProvider;
        private readonly IByteArrayConvertor<T> _byteArrayConvertor;
        private readonly IPairConcatStrategy _pairConcatStrategy;
        public List<T> Leaves => _leaves;

        public byte[] Root { get; private set; }

        public LeanIncrementalMerkleTree(
            IHashProvider hashProvider,
            IByteArrayConvertor<T> byteArrayConvertor,
            PairingConcatType pairingConcatType = PairingConcatType.Normal)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _byteArrayConvertor = byteArrayConvertor ?? throw new ArgumentNullException(nameof(byteArrayConvertor));
            _pairConcatStrategy = PairingConcatFactory.GetPairConcatStrategy(pairingConcatType);
#if NET5_0_OR_GREATER
            Root = Array.Empty<byte>();
#else
            Root = new byte[0];
#endif
        }

        public void InsertLeaf(T leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            _leaves.Add(leaf);
            UpdateRootIncrementally();
        }

        public void InsertMany(IEnumerable<T> leaves)
        {
            if (leaves == null) throw new ArgumentNullException(nameof(leaves));
            var list = leaves.ToList();
            if (list.Count == 0) return;
            _leaves.AddRange(list);
            UpdateRootIncrementally();
        }

        public void Update(int index, T newLeaf)
        {
            if (newLeaf == null) throw new ArgumentNullException(nameof(newLeaf));
            if (index < 0 || index >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range");

            _leaves[index] = newLeaf;
            UpdateRootIncrementally();
        }

        public void UpdateMany(int[] indices, T[] newLeaves)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (newLeaves == null) throw new ArgumentNullException(nameof(newLeaves));
            if (indices.Length != newLeaves.Length)
                throw new ArgumentException("The number of indices must match the number of leaves");
            if (indices.Length == 0) return;

            var seenIndices = new HashSet<int>();
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] < 0 || indices[i] >= _leaves.Count)
                    throw new ArgumentOutOfRangeException(nameof(indices), $"Index {indices[i]} at position {i} is out of range");
                if (!seenIndices.Add(indices[i]))
                    throw new ArgumentException($"Duplicate index {indices[i]} at position {i}");
                if (newLeaves[i] == null)
                    throw new ArgumentNullException(nameof(newLeaves), $"Leaf at position {i} is null");
            }

            for (int i = 0; i < indices.Length; i++)
            {
                _leaves[indices[i]] = newLeaves[i];
            }
            UpdateRootIncrementally();
        }

        public bool Has(T leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            return _leaves.Contains(leaf);
        }

        public int IndexOf(T leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            return _leaves.IndexOf(leaf);
        }

        public string Export(Func<byte[], string> nodeFormatter = null)
        {
            nodeFormatter = nodeFormatter ?? (bytes => Convert.ToBase64String(bytes));
            var nodes = new List<List<string>>();
            var leavesAsBytes = _leaves.Select(_byteArrayConvertor.ConvertToByteArray).ToList();
            nodes.Add(leavesAsBytes.Select(nodeFormatter).ToList());

            var currentLevel = leavesAsBytes;
            while (currentLevel.Count > 1)
            {
                var nextLevel = PairwiseHash(currentLevel);
                nodes.Add(nextLevel.Select(nodeFormatter).ToList());
                currentLevel = nextLevel;
            }

#if NET5_0_OR_GREATER
            return JsonSerializer.Serialize(nodes);
#else
            return JsonConvert.SerializeObject(nodes);
#endif
        }

        public static LeanIncrementalMerkleTree<T> Import(
            IHashProvider hashProvider,
            IByteArrayConvertor<T> byteArrayConvertor,
            string json,
            Func<string, T> leafMapper,
            Func<string, byte[]> nodeParser = null,
            PairingConcatType pairingConcatType = PairingConcatType.Normal)
        {
            if (hashProvider == null) throw new ArgumentNullException(nameof(hashProvider));
            if (byteArrayConvertor == null) throw new ArgumentNullException(nameof(byteArrayConvertor));
            if (string.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));
            if (leafMapper == null) throw new ArgumentNullException(nameof(leafMapper));

            nodeParser = nodeParser ?? (str => Convert.FromBase64String(str));
            var tree = new LeanIncrementalMerkleTree<T>(hashProvider, byteArrayConvertor, pairingConcatType);

            try
            {
#if NET5_0_OR_GREATER
                var nodes = JsonSerializer.Deserialize<List<List<string>>>(json);
#else
                var nodes = JsonConvert.DeserializeObject<List<List<string>>>(json);
#endif
                if (nodes == null || nodes.Count == 0 || nodes[0].Count == 0)
                    throw new ArgumentException("Invalid JSON: empty or malformed tree data");

                var leaves = nodes[0].Select(leafMapper).ToList();
                tree._leaves.AddRange(leaves);
                tree.UpdateRootIncrementally();

                // Validate upper levels if nodeParser is provided
                if (nodeParser != null && nodes.Count > 1)
                {
                    var computedNodes = new List<List<byte[]>> { tree._leaves.Select(byteArrayConvertor.ConvertToByteArray).ToList() };
                    var currentLevel = computedNodes[0];
                    while (currentLevel.Count > 1)
                    {
                        computedNodes.Add(tree.PairwiseHash(currentLevel));
                        currentLevel = computedNodes.Last(); ;
                    }

                    for (int level = 1; level < nodes.Count; level++)
                    {
                        var expectedNodes = nodes[level].Select(nodeParser).ToList();
                        if (level < computedNodes.Count)
                        {
                            var computedLevel = computedNodes[level];
                            if (!expectedNodes.SequenceEqual(computedLevel, new ByteArrayComparer()))
                                throw new ArgumentException($"Invalid tree data: mismatch at level {level}");
                        }
                    }
                }
            }
#if NET5_0_OR_GREATER
            catch (System.Text.Json.JsonException ex)
#else
            catch (JsonException ex)
#endif
            {
                throw new ArgumentException("Invalid JSON format", nameof(json), ex);
            }

            return tree;
        }

        private void UpdateRootIncrementally()
        {
            var nodes = _leaves.Select(_byteArrayConvertor.ConvertToByteArray).ToList();
            while (nodes.Count > 1)
            {
                var temp = new List<byte[]>();
                for (int i = 0; i < nodes.Count; i += 2)
                {
                    if (i + 1 < nodes.Count)
                        temp.Add(HashPair(nodes[i], nodes[i + 1]));
                    else
                        temp.Add(nodes[i]);
                }
                nodes = temp;
            }
#if NET5_0_OR_GREATER
            Root = nodes.FirstOrDefault() ?? Array.Empty<byte>();
#else
            Root = nodes.FirstOrDefault() ?? new byte[0];
#endif
        }

        public MerkleProof GenerateProof(int leafIndex)
        {
            if (leafIndex < 0 || leafIndex >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(leafIndex));
            var proofNodes = new List<byte[]>();
            int index = leafIndex;
            var currentLevel = _leaves.Select(_byteArrayConvertor.ConvertToByteArray).ToList();
            for (int level = 0; currentLevel.Count > 1; level++)
            {
                int siblingIndex = index ^ 1;
                if (siblingIndex < currentLevel.Count)
                    proofNodes.Add(currentLevel[siblingIndex]);
                index /= 2;
                currentLevel = PairwiseHash(currentLevel);
            }
            return new MerkleProof { ProofNodes = proofNodes };
        }

        public bool VerifyProof(MerkleProof proof, T leaf, byte[] root)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            if (root == null) throw new ArgumentNullException(nameof(root));
            var computedHash = _byteArrayConvertor.ConvertToByteArray(leaf);
            foreach (var node in proof.ProofNodes)
            {
                computedHash = HashPair(computedHash, node);
            }
            return computedHash.SequenceEqual(root);
        }

        private byte[] HashPair(byte[] left, byte[] right)
        {
            var combined = _pairConcatStrategy.Concat(left, right);
            return _hashProvider.ComputeHash(combined);
        }

        private List<byte[]> PairwiseHash(List<byte[]> nodes)
        {
            var nextLevel = new List<byte[]>();
            for (int i = 0; i < nodes.Count; i += 2)
            {
                if (i + 1 < nodes.Count)
                    nextLevel.Add(HashPair(nodes[i], nodes[i + 1]));
                else
                    nextLevel.Add(nodes[i]);
            }
            return nextLevel;
        }

        public int Size => _leaves.Count;

        public int Depth
        {
            get
            {
                if (_leaves.Count == 0) return 0;
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
                return (int)Math.Ceiling(Math.Log2(_leaves.Count));
#else
                return (int)Math.Ceiling(Log2(_leaves.Count));
#endif
            }
        }

        // Fallback Log2 implementation for older .NET versions
#if !(NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER)
        private static double Log2(double x)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Value must be positive");
            return Math.Log(x, 2);
        }
#endif
    }

}