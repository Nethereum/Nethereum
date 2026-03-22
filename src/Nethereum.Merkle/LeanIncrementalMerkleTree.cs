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
        private readonly bool _hashLeafOnInsert;
        private readonly ILeanIMTNodeStorage _storage;
        public IReadOnlyList<T> Leaves => _leaves;

        public byte[] Root { get; private set; }

        public LeanIncrementalMerkleTree(
            IHashProvider hashProvider,
            IByteArrayConvertor<T> byteArrayConvertor,
            PairingConcatType pairingConcatType = PairingConcatType.Normal,
            bool hashLeafOnInsert = true,
            ILeanIMTNodeStorage storage = null)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _byteArrayConvertor = byteArrayConvertor ?? throw new ArgumentNullException(nameof(byteArrayConvertor));
            _pairConcatStrategy = PairingConcatFactory.GetPairConcatStrategy(pairingConcatType);
            _hashLeafOnInsert = hashLeafOnInsert;
            _storage = storage ?? new InMemoryLeanIMTNodeStorage();
#if NET5_0_OR_GREATER
            Root = Array.Empty<byte>();
#else
            Root = new byte[0];
#endif
        }

        public ILeanIMTNodeStorage Storage => _storage;

        public void InsertLeaf(T leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            _leaves.Add(leaf);
            var leafBytes = _byteArrayConvertor.ConvertToByteArray(leaf);
            var hash = _hashLeafOnInsert ? _hashProvider.ComputeHash(leafBytes) : leafBytes;

            if (_storage.GetLevelCount() == 0 || (_storage.GetLevelCount() == 1 && _storage.GetNodeCount(0) == 0))
            {
                _storage.EnsureLevel(0);
                _storage.SetNode(0, 0, hash);
                Root = hash;
                return;
            }

            int leafIndex = _storage.GetNodeCount(0);
            _storage.SetNode(0, leafIndex, hash);

            int depth = Depth;
            while (_storage.GetLevelCount() <= depth)
                _storage.EnsureLevel(_storage.GetLevelCount());

            PropagateInsert(leafIndex);
        }

        public void InsertMany(IEnumerable<T> leaves)
        {
            if (leaves == null) throw new ArgumentNullException(nameof(leaves));
            var list = leaves.ToList();
            if (list.Count == 0) return;

            int startIndex = _leaves.Count >> 1;

            _leaves.AddRange(list);

            var leafHashes = new List<byte[]>(list.Count);
            foreach (var leaf in list)
            {
                var bytes = _byteArrayConvertor.ConvertToByteArray(leaf);
                leafHashes.Add(_hashLeafOnInsert ? _hashProvider.ComputeHash(bytes) : bytes);
            }

            int existingLeafCount = _storage.GetNodeCount(0);
            _storage.EnsureLevel(0);
            for (int i = 0; i < leafHashes.Count; i++)
            {
                _storage.SetNode(0, existingLeafCount + i, leafHashes[i]);
            }

            int totalLeaves = _leaves.Count;
            int requiredDepth = totalLeaves <= 1 ? 0 : (int)Math.Ceiling(Log2(totalLeaves));
            while (_storage.GetLevelCount() <= requiredDepth)
                _storage.EnsureLevel(_storage.GetLevelCount());

            int depth = _storage.GetLevelCount() - 1;
            for (int level = 0; level < depth; level++)
            {
                int levelNodeCount = _storage.GetNodeCount(level);
                int numberOfParents = (levelNodeCount + 1) / 2;

                for (int index = startIndex; index < numberOfParents; index++)
                {
                    var left = _storage.GetNode(level, index * 2);
                    var right = (index * 2 + 1 < levelNodeCount)
                        ? _storage.GetNode(level, index * 2 + 1)
                        : null;

                    var parentHash = right != null ? HashPair(left, right) : left;
                    _storage.SetNode(level + 1, index, parentHash);
                }

                startIndex >>= 1;
            }

            Root = _storage.GetNode(depth, 0);
        }

        public void Update(int index, T newLeaf)
        {
            if (newLeaf == null) throw new ArgumentNullException(nameof(newLeaf));
            if (index < 0 || index >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range");

            _leaves[index] = newLeaf;
            var newBytes = _byteArrayConvertor.ConvertToByteArray(newLeaf);
            _storage.SetNode(0, index, _hashLeafOnInsert ? _hashProvider.ComputeHash(newBytes) : newBytes);
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
                var bytes = _byteArrayConvertor.ConvertToByteArray(newLeaves[i]);
                _storage.SetNode(0, indices[i], _hashLeafOnInsert ? _hashProvider.ComputeHash(bytes) : bytes);
            }

            var modifiedParents = new HashSet<int>();
            foreach (var idx in indices)
                modifiedParents.Add(idx >> 1);

            int depth = _storage.GetLevelCount() - 1;
            for (int level = 1; level <= depth; level++)
            {
                var nextModified = new HashSet<int>();
                foreach (var parentIdx in modifiedParents)
                {
                    var left = _storage.GetNode(level - 1, parentIdx * 2);
                    var right = _storage.GetNode(level - 1, parentIdx * 2 + 1);
                    var parentHash = right != null ? HashPair(left, right) : left;
                    _storage.SetNode(level, parentIdx, parentHash);
                    nextModified.Add(parentIdx >> 1);
                }
                modifiedParents = nextModified;
            }

            Root = _storage.GetNode(depth, 0);
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

            int levelCount = _storage.GetLevelCount();
            if (levelCount == 0)
            {
                nodes.Add(new List<string>());
            }
            else
            {
                for (int level = 0; level < levelCount; level++)
                {
                    var levelNodes = new List<string>();
                    int count = _storage.GetNodeCount(level);
                    for (int i = 0; i < count; i++)
                    {
                        var node = _storage.GetNode(level, i);
                        levelNodes.Add(node != null ? nodeFormatter(node) : "");
                    }
                    nodes.Add(levelNodes);
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
            PairingConcatType pairingConcatType = PairingConcatType.Normal,
            bool hashLeafOnInsert = true,
            ILeanIMTNodeStorage storage = null,
            bool verifyIntegrity = true)
        {
            if (hashProvider == null) throw new ArgumentNullException(nameof(hashProvider));
            if (byteArrayConvertor == null) throw new ArgumentNullException(nameof(byteArrayConvertor));
            if (string.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));
            if (leafMapper == null) throw new ArgumentNullException(nameof(leafMapper));

            nodeParser = nodeParser ?? (str => Convert.FromBase64String(str));
            var tree = new LeanIncrementalMerkleTree<T>(hashProvider, byteArrayConvertor, pairingConcatType, hashLeafOnInsert, storage);

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

                tree._storage.Clear();
                for (int level = 0; level < nodes.Count; level++)
                {
                    tree._storage.EnsureLevel(level);
                    for (int i = 0; i < nodes[level].Count; i++)
                    {
                        var nodeBytes = nodeParser(nodes[level][i]);
                        tree._storage.SetNode(level, i, nodeBytes);
                    }
                }

                var lastLevel = nodes.Count - 1;
                tree.Root = tree._storage.GetNode(lastLevel, 0)
#if NET5_0_OR_GREATER
                    ?? Array.Empty<byte>();
#else
                    ?? new byte[0];
#endif

                if (verifyIntegrity)
                {
                    try
                    {
                        tree.VerifyStorageIntegrity();
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new ArgumentException("Invalid tree data: " + ex.Message, nameof(json), ex);
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

        public void VerifyStorageIntegrity()
        {
            if (_leaves.Count == 0) return;

            for (int i = 0; i < _leaves.Count; i++)
            {
                var leafBytes = _byteArrayConvertor.ConvertToByteArray(_leaves[i]);
                var expectedHash = _hashLeafOnInsert ? _hashProvider.ComputeHash(leafBytes) : leafBytes;
                var storedHash = _storage.GetNode(0, i);
                if (storedHash == null || !expectedHash.SequenceEqual(storedHash))
                    throw new InvalidOperationException($"Storage integrity check failed at leaf index {i}");
            }

            int depth = _storage.GetLevelCount() - 1;
            for (int level = 0; level < depth; level++)
            {
                int count = _storage.GetNodeCount(level);
                int parentCount = (count + 1) / 2;
                for (int i = 0; i < parentCount; i++)
                {
                    var left = _storage.GetNode(level, i * 2);
                    var right = (i * 2 + 1 < count) ? _storage.GetNode(level, i * 2 + 1) : null;
                    var expected = right != null ? HashPair(left, right) : left;
                    var stored = _storage.GetNode(level + 1, i);
                    if (stored == null || !expected.SequenceEqual(stored))
                        throw new InvalidOperationException($"Storage integrity check failed at level {level + 1}, index {i}");
                }
            }
        }

        private void PropagateInsert(int leafIndex)
        {
            int idx = leafIndex;
            int depth = _storage.GetLevelCount() - 1;

            for (int level = 0; level < depth; level++)
            {
                int parentIdx = idx / 2;
                int leftIdx = parentIdx * 2;
                int rightIdx = leftIdx + 1;

                var left = _storage.GetNode(level, leftIdx);
                var right = _storage.GetNode(level, rightIdx);

                var parentHash = right != null ? HashPair(left, right) : left;
                _storage.SetNode(level + 1, parentIdx, parentHash);

                idx = parentIdx;
            }

            var topNode = _storage.GetNode(depth, 0);
            if (_storage.GetNodeCount(depth) > 1)
            {
                int topCount = _storage.GetNodeCount(depth);
                int newDepth = depth + 1;
                _storage.EnsureLevel(newDepth);

                int parentCount = (topCount + 1) / 2;
                for (int i = 0; i < parentCount; i++)
                {
                    var left = _storage.GetNode(depth, i * 2);
                    var right = (i * 2 + 1 < topCount) ? _storage.GetNode(depth, i * 2 + 1) : null;
                    _storage.SetNode(newDepth, i, right != null ? HashPair(left, right) : left);
                }
                Root = _storage.GetNode(newDepth, 0);
            }
            else
            {
                Root = topNode;
            }
        }

        private void PropagateFromIndex(int leafIndex)
        {
            int depth = _storage.GetLevelCount() - 1;
            if (depth <= 0)
            {
                Root = _storage.GetNode(0, 0);
                return;
            }

            int idx = leafIndex;
            for (int level = 0; level < depth; level++)
            {
                int parentIdx = idx / 2;
                int leftIdx = parentIdx * 2;
                int rightIdx = leftIdx + 1;

                var left = _storage.GetNode(level, leftIdx);
                var right = _storage.GetNode(level, rightIdx);

                var parentHash = right != null ? HashPair(left, right) : left;
                _storage.SetNode(level + 1, parentIdx, parentHash);

                idx = parentIdx;
            }
            Root = _storage.GetNode(depth, 0);
        }

        public MerkleProof GenerateProof(int leafIndex)
        {
            if (leafIndex < 0 || leafIndex >= _leaves.Count)
                throw new ArgumentOutOfRangeException(nameof(leafIndex));

            var proofNodes = new List<byte[]>();
            int idx = leafIndex;
            int depth = _storage.GetLevelCount() - 1;

            for (int level = 0; level < depth; level++)
            {
                int siblingIdx = idx ^ 1;
                var sibling = _storage.GetNode(level, siblingIdx);
                if (sibling != null)
                    proofNodes.Add(sibling);
                idx /= 2;
            }

            var pathIndices = new List<int>();
            int pathIdx = leafIndex;
            for (int level = 0; level < depth; level++)
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
            var leafBytes = _byteArrayConvertor.ConvertToByteArray(leaf);
            var computedHash = _hashLeafOnInsert ? _hashProvider.ComputeHash(leafBytes) : leafBytes;
            for (int i = 0; i < proof.ProofNodes.Count; i++)
            {
                var node = proof.ProofNodes[i];
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

        public int Size => _leaves.Count;

        public int Depth
        {
            get
            {
                if (_leaves.Count == 0) return 0;
                return (int)Math.Ceiling(Log2(_leaves.Count));
            }
        }

        private static double Log2(double x)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Value must be positive");
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
            return Math.Log2(x);
#else
            return Math.Log(x, 2);
#endif
        }
    }

}
