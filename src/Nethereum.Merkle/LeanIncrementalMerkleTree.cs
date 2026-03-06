#if NET5_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private List<List<byte[]>> _layers = new List<List<byte[]>>();
        public IReadOnlyList<T> Leaves => _leaves;

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
            var hash = _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(leaf));

            if (_layers.Count == 0)
            {
                _layers.Add(new List<byte[]> { hash });
                Root = hash;
                return;
            }

            _layers[0].Add(hash);
            PropagateInsert();
        }

        public void InsertMany(IEnumerable<T> leaves)
        {
            if (leaves == null) throw new ArgumentNullException(nameof(leaves));
            var list = leaves.ToList();
            if (list.Count == 0) return;
            _leaves.AddRange(list);
            RebuildLayers();
        }

        public void Update(int index, T newLeaf)
        {
            if (newLeaf == null) throw new ArgumentNullException(nameof(newLeaf));
            if (index < 0 || index >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range");

            _leaves[index] = newLeaf;
            _layers[0][index] = _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(newLeaf));
            PropagateFromIndex(index);
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
            RebuildLayers();
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

            if (_layers.Count == 0)
            {
                nodes.Add(new List<string>());
            }
            else
            {
                for (int i = 0; i < _layers.Count; i++)
                {
                    nodes.Add(_layers[i].Select(nodeFormatter).ToList());
                }
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
                tree.RebuildLayers();

                if (nodeParser != null && nodes.Count > 1)
                {
                    for (int level = 1; level < nodes.Count; level++)
                    {
                        var expectedNodes = nodes[level].Select(nodeParser).ToList();
                        if (level < tree._layers.Count)
                        {
                            var computedLevel = tree._layers[level];
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

        private void RebuildLayers()
        {
            _layers.Clear();
            if (_leaves.Count == 0)
            {
#if NET5_0_OR_GREATER
                Root = Array.Empty<byte>();
#else
                Root = new byte[0];
#endif
                return;
            }

            _layers.Add(_leaves.Select(l => _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(l))).ToList());
            var current = _layers[0];
            while (current.Count > 1)
            {
                var next = PairwiseHash(current);
                _layers.Add(next);
                current = next;
            }
            Root = current[0];
        }

        private void PropagateInsert()
        {
            int leafCount = _layers[0].Count;

            for (int level = 0; level < _layers.Count; level++)
            {
                var currentLayer = _layers[level];
                int nextLevelCount = (currentLayer.Count + 1) / 2;

                if (level + 1 < _layers.Count)
                {
                    var nextLayer = _layers[level + 1];
                    while (nextLayer.Count < nextLevelCount)
                        nextLayer.Add(null);

                    int lastIdx = currentLayer.Count - 1;
                    int parentIdx = lastIdx / 2;
                    int leftIdx = parentIdx * 2;
                    int rightIdx = leftIdx + 1;

                    byte[] parentHash;
                    if (rightIdx < currentLayer.Count)
                        parentHash = HashPair(currentLayer[leftIdx], currentLayer[rightIdx]);
                    else
                        parentHash = currentLayer[leftIdx];

                    nextLayer[parentIdx] = parentHash;
                }
                else if (currentLayer.Count > 1)
                {
                    var next = new List<byte[]>(new byte[nextLevelCount][]);
                    int lastIdx = currentLayer.Count - 1;
                    int parentIdx = lastIdx / 2;

                    for (int p = 0; p <= parentIdx; p++)
                    {
                        int li = p * 2;
                        int ri = li + 1;
                        if (ri < currentLayer.Count)
                            next[p] = HashPair(currentLayer[li], currentLayer[ri]);
                        else
                            next[p] = currentLayer[li];
                    }

                    _layers.Add(next);
                }
            }

            var topLayer = _layers[_layers.Count - 1];
            if (topLayer.Count > 1)
            {
                var next = PairwiseHash(topLayer);
                _layers.Add(next);
            }
            Root = _layers[_layers.Count - 1][0];
        }

        private void PropagateFromIndex(int leafIndex)
        {
            if (_layers.Count <= 1)
            {
                Root = _layers[0][0];
                return;
            }

            int idx = leafIndex;
            for (int level = 0; level < _layers.Count - 1; level++)
            {
                int parentIdx = idx / 2;
                int leftIdx = parentIdx * 2;
                int rightIdx = leftIdx + 1;

                byte[] parentHash;
                if (rightIdx < _layers[level].Count)
                    parentHash = HashPair(_layers[level][leftIdx], _layers[level][rightIdx]);
                else
                    parentHash = _layers[level][leftIdx];

                _layers[level + 1][parentIdx] = parentHash;
                idx = parentIdx;
            }
            Root = _layers[_layers.Count - 1][0];
        }

        public MerkleProof GenerateProof(int leafIndex)
        {
            if (leafIndex < 0 || leafIndex >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(leafIndex));
            var proofNodes = new List<byte[]>();
            int idx = leafIndex;
            for (int level = 0; level < _layers.Count - 1; level++)
            {
                int siblingIdx = idx ^ 1;
                if (siblingIdx < _layers[level].Count)
                    proofNodes.Add(_layers[level][siblingIdx]);
                idx /= 2;
            }
            var pathIndices = new List<int>();
            int pathIdx = leafIndex;
            for (int level = 0; level < _layers.Count - 1; level++)
            {
                pathIndices.Add(pathIdx % 2);
                pathIdx /= 2;
            }
            return new MerkleProof { ProofNodes = proofNodes, PathIndices = pathIndices };
        }

        public bool VerifyProof(MerkleProof proof, T leaf, byte[] root)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            if (root == null) throw new ArgumentNullException(nameof(root));
            var computedHash = _hashProvider.ComputeHash(_byteArrayConvertor.ConvertToByteArray(leaf));
            for (int i = 0; i < proof.ProofNodes.Count; i++)
            {
                var node = proof.ProofNodes[i];
                // PathIndices[i] == 0 means current hash is on the left
                if (proof.PathIndices != null && i < proof.PathIndices.Count && proof.PathIndices[i] == 1)
                    computedHash = HashPair(node, computedHash);
                else
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

#if !(NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER)
        private static double Log2(double x)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Value must be positive");
            return Math.Log(x, 2);
        }
#endif
    }

}
