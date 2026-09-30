using Nethereum.Util;
using Nethereum.Util.HashProviders;
using System;
using System.Collections.Generic;


using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia
{
    public class PatriciaTrie
    {
        public PatriciaTrie(IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            Root = EmptyNode.Instance;
        }

        public PatriciaTrie(byte[] hashRoot, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            Root = new HashNode(hashProvider) { Hash = hashRoot };
        }

        public PatriciaTrie(byte[] hashRoot):this(hashRoot, Sha3KeccackHashProvider.Instance)
        {

        }

        public PatriciaTrie():this(Sha3KeccackHashProvider.Instance)
        {

        }

        public PatriciaTrie(Node root, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            Root = root ?? new EmptyNode(hashProvider);
        }

        public PatriciaTrie(Node root) : this(root, Sha3KeccackHashProvider.Instance)
        {
        }


        public PatriciaTrie(ITrieNodeStore store, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            Root = EmptyNode.Instance;
        }

        public PatriciaTrie(ITrieNodeStore store) : this(store, Sha3KeccackHashProvider.Instance)
        {
        }

        public PatriciaTrie(byte[] hashRoot, ITrieNodeStore store, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            Root = new HashNode(hashProvider) { Hash = hashRoot, Path = new byte[0] };
        }

        public PatriciaTrie(byte[] hashRoot, ITrieNodeStore store) : this(hashRoot, store, Sha3KeccackHashProvider.Instance)
        {
        }

        public PatriciaTrie(Node root, ITrieNodeStore store, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            Root = root ?? new EmptyNode(hashProvider);
        }

        public PatriciaTrie(Node root, ITrieNodeStore store) : this(root, store, Sha3KeccackHashProvider.Instance)
        {
        }

        public PatriciaTrie(ITrieNodeStore store, byte[] owner, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            _owner = owner;
            Root = EmptyNode.Instance;
        }

        public PatriciaTrie(ITrieNodeStore store, byte[] owner) : this(store, owner, Sha3KeccackHashProvider.Instance)
        {
        }

        public PatriciaTrie(byte[] hashRoot, ITrieNodeStore store, byte[] owner, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            _owner = owner;
            Root = new HashNode(hashProvider) { Hash = hashRoot, Owner = owner, Path = new byte[0] };
        }

        public PatriciaTrie(byte[] hashRoot, ITrieNodeStore store, byte[] owner) : this(hashRoot, store, owner, Sha3KeccackHashProvider.Instance)
        {
        }

        public PatriciaTrie(Node root, ITrieNodeStore store, byte[] owner, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            _store = store;
            _owner = owner;
            Root = root ?? new EmptyNode(hashProvider);
        }

        public PatriciaTrie(Node root, ITrieNodeStore store, byte[] owner) : this(root, store, owner, Sha3KeccackHashProvider.Instance)
        {
        }


        public static PatriciaTrie LoadFromStorage(byte[] rootHash, ITrieNodeStore store)
            => LoadFromStorage(rootHash, store, Sha3KeccackHashProvider.Instance);

        public static PatriciaTrie LoadFromStorage(byte[] rootHash, ITrieNodeStore store, IHashProvider hashProvider)
        {
            if (rootHash == null || rootHash.Length == 0)
                return new PatriciaTrie(store, hashProvider);
            return new PatriciaTrie(rootHash, store, hashProvider);
        }

        public static PatriciaTrie LoadFromStorage(byte[] rootHash, ITrieNodeStore store, byte[] owner)
            => LoadFromStorage(rootHash, store, owner, Sha3KeccackHashProvider.Instance);

        public static PatriciaTrie LoadFromStorage(byte[] rootHash, ITrieNodeStore store, byte[] owner, IHashProvider hashProvider)
        {
            if (rootHash == null || rootHash.Length == 0)
                return new PatriciaTrie(store, owner, hashProvider);
            return new PatriciaTrie(rootHash, store, owner, hashProvider);
        }

        public static PatriciaTrie ReattachFromRawRoot(IRawNodeReader rawReader, ITrieNodeStore store, byte[] owner, IHashProvider hashProvider)
        {
            if (rawReader == null) throw new ArgumentNullException(nameof(rawReader));
            var ownerKey = owner ?? new byte[0];
            var rootRlp = rawReader.TryGetRawNode(ownerKey, new byte[0]);
            if (rootRlp == null || rootRlp.Length == 0) return null;
            var root = NodeRlpDecoder.Decode(rootRlp, ownerKey, new byte[0], hashProvider);
            return new PatriciaTrie(root, store, owner, hashProvider);
        }

        public static PatriciaTrie ReattachFromRawRoot(IRawNodeReader rawReader, ITrieNodeStore store, byte[] owner)
            => ReattachFromRawRoot(rawReader, store, owner, Sha3KeccackHashProvider.Instance);

        public IHashProvider HashProvider { get; }

        public Node Root { get; private set; }

        private ITrieNodeStore _store;

        public ITrieNodeStore Store => _store;

        private byte[] _owner;

        public ITrieTracer Tracer { get; set; }

        private void ReloadInner(HashNode hashNode)
        {
            if (hashNode.InnerNode == null) hashNode.DecodeInnerNode(_store, false);
        }

        private byte[] ChildPath(byte[] path, byte[] suffix)
            => Tracer == null ? path : path.ConcatArrays(suffix);

        public byte[] Get(byte[] key) => Get(Root, key.ConvertToNibbles());

        public byte[] Get(Node node, byte[] keyAsNibbles)
        {
            if (node is null || node is EmptyNode)
            {
                return null;
            }

            if (node is LeafNode leafNode)
            {
                return GetFromLeafNode(leafNode, keyAsNibbles);
            }

            if (node is BranchNode branchNode)
            {
                return GetFromBranchNode(branchNode, keyAsNibbles);
            }

            if (node is ExtendedNode extendedNode)
            {
                return GetFromExtendedNode(extendedNode, keyAsNibbles);
            }

            if (node is HashNode hashNode)
            {
                return GetFromHashNode(keyAsNibbles, hashNode);
            }

            return null;
        }

        public byte[] GetFromHashNode(byte[] keyAsNibbles, HashNode hashNode)
        {
            ReloadInner(hashNode);
            return Get(hashNode.InnerNode, keyAsNibbles);
        }

        public byte[] GetFromExtendedNode(ExtendedNode currentNode, byte[] keyAsNibbles)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (currentNode.Nibbles.Length > foundSameNibbles.Length) return null;
            return Get(currentNode.InnerNode, keyAsNibbles.SliceFrom(foundSameNibbles.Length));
        }

        public byte[] GetFromLeafNode(LeafNode currentNode, byte[] keyAsNibbles)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var areLeafNodeNibblesAndKeyNibblesTheSame =
               (foundSameNibbles.Length == currentNode.Nibbles.Length && foundSameNibbles.Length == keyAsNibbles.Length);
            if (areLeafNodeNibblesAndKeyNibblesTheSame)
            {
                return currentNode.Value;
            }

            return null;
        }

        public byte[] GetFromBranchNode(BranchNode currentNode, byte[] keyAsNibbles)
        {
            if(keyAsNibbles == null || keyAsNibbles.Length == 0)
            {
                return currentNode.Value;
            }
            return Get(currentNode.Children[keyAsNibbles[0]], keyAsNibbles.SliceFrom(1));
        }

        public Node Put(Node node, byte[] keyAsNibbles, byte[] value) => Put(node, keyAsNibbles, value, new byte[0]);

        private Node Put(Node node, byte[] keyAsNibbles, byte[] value, byte[] path)
        {
            if (node is null || node is EmptyNode)
            {
                return PutOnAnExistingEmptyNode(keyAsNibbles, value);
            }

            if (node is LeafNode leafNode)
            {
                return PutOnAnExistingLeafNode(leafNode, keyAsNibbles, value, path);
            }

            if (node is BranchNode branchNode)
            {
                return PutOnAnExistingBranchNode(branchNode, keyAsNibbles, value, path);
            }

            if (node is ExtendedNode extendedNode)
            {
                return PutOnAnExistingExtendedNode(extendedNode, keyAsNibbles, value, path);
            }

            if (node is HashNode hashNode)
            {
                return PutOnAnExistingHashNode(hashNode, keyAsNibbles, value, path);
            }

            return null;

        }

        private Node PutOnAnExistingHashNode(HashNode hashNode, byte[] keyAsNibbles, byte[] value, byte[] path)
        {
            ReloadInner(hashNode);
            hashNode.InnerNode = Put(hashNode.InnerNode, keyAsNibbles, value, path);
            return hashNode;
        }

        private Node PutOnAnExistingExtendedNode(ExtendedNode currentNode, byte[] keyAsNibbles, byte[] value, byte[] path)
        {

            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var extendedNodeHasNonSameNibbles = foundSameNibbles.Length < currentNode.Nibbles.Length;

            if (extendedNodeHasNonSameNibbles)
            {
                var newBranchCurrentNodeNibble = currentNode.Nibbles[foundSameNibbles.Length];
                var currentNodeNonSameNibbles = currentNode.Nibbles.SliceFrom(foundSameNibbles.Length + 1);
                var newBranch = new BranchNode(HashProvider);
                if (currentNodeNonSameNibbles.Length > 0)
                {
                    var extendedNode = new ExtendedNode();
                    extendedNode.Nibbles = currentNodeNonSameNibbles;
                    extendedNode.InnerNode = currentNode.InnerNode;
                    newBranch.SetChild(newBranchCurrentNodeNibble, extendedNode);
                }
                else
                {
                    newBranch.SetChild(newBranchCurrentNodeNibble, currentNode.InnerNode);

                }

                var keyHasMoreNibblesThanFoundTheSame = foundSameNibbles.Length < keyAsNibbles.Length;

                if (keyHasMoreNibblesThanFoundTheSame)
                {
                    var newLeafBranchNibble = keyAsNibbles[foundSameNibbles.Length];
                    var keyNonSameNibbles = keyAsNibbles.SliceFrom(foundSameNibbles.Length + 1);
                    var newLeafValue = new LeafNode(HashProvider);
                    newLeafValue.Value = value;
                    newLeafValue.Nibbles = keyNonSameNibbles;
                    newBranch.SetChild(newLeafBranchNibble, newLeafValue);
                }
                else
                {
                    var keyHasTheSameFoundNibbles = foundSameNibbles.Length == keyAsNibbles.Length;
                    if (keyHasTheSameFoundNibbles)
                    {
                        newBranch.Value = value;
                    }

                }

                if (foundSameNibbles.Length == 0)
                {
                    return newBranch;
                }
                else
                {
                    return new ExtendedNode(HashProvider) { Nibbles = foundSameNibbles, InnerNode = newBranch };
                }

            }
            else
            {
                currentNode.InnerNode = Put(currentNode.InnerNode, keyAsNibbles.SliceFrom(foundSameNibbles.Length), value, ChildPath(path, currentNode.Nibbles));
                return currentNode;
            }

        }

        private Node PutOnAnExistingBranchNode(BranchNode currentNode, byte[] keyAsNibbles, byte[] value, byte[] path)
        {
            if(keyAsNibbles == null || keyAsNibbles.Length == 0)
            {
                currentNode.Value = value;
                return currentNode;
            }

            var nibbleBranch = keyAsNibbles[0];
            var newChild = Put(currentNode.Children[nibbleBranch], keyAsNibbles.SliceFrom(1), value, ChildPath(path, new byte[] { nibbleBranch }));
            currentNode.SetChild(nibbleBranch, newChild);
            return currentNode;
        }


        private Node PutOnAnExistingLeafNode(LeafNode currentNode, byte[] keyAsNibbles, byte[] value, byte[] path)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var areLeafNodeNibblesAndKeyNibblesTheSame =
                (foundSameNibbles.Length == currentNode.Nibbles.Length && foundSameNibbles.Length == keyAsNibbles.Length);

            if (areLeafNodeNibblesAndKeyNibblesTheSame)
            {
                currentNode.Value = value;
                return currentNode;
            }

            var branchNode = new BranchNode(HashProvider);

            var allTheLeafNodeNibblesFoundTheSameAreIncludedButKeyNibblesHasMore = (currentNode.Nibbles.Length == foundSameNibbles.Length) && !areLeafNodeNibblesAndKeyNibblesTheSame;
            var allTheKeyNibblesFoundTheSameAreIncludedButLeafNodeHasMore = (keyAsNibbles.Length == foundSameNibbles.Length) && !areLeafNodeNibblesAndKeyNibblesTheSame;
            var keyNibblesHasMoreNibblesThanFoundTheSame = keyAsNibbles.Length > foundSameNibbles.Length;
            var leafNodeHasMoreNibblesThanFoundTheSame = currentNode.Nibbles.Length > foundSameNibbles.Length;

            if (allTheLeafNodeNibblesFoundTheSameAreIncludedButKeyNibblesHasMore)
            {
                branchNode.Value = currentNode.Value;
            }

            if (keyNibblesHasMoreNibblesThanFoundTheSame)
            {
                var newLeafNode = new LeafNode(HashProvider);
                newLeafNode.Value = value;
                newLeafNode.Nibbles = keyAsNibbles.SliceFrom(foundSameNibbles.Length + 1);
                branchNode.SetChild(keyAsNibbles[foundSameNibbles.Length], newLeafNode);
            }

            if (allTheKeyNibblesFoundTheSameAreIncludedButLeafNodeHasMore)
            {
                branchNode.Value = value;

            }

            if (leafNodeHasMoreNibblesThanFoundTheSame)
            {
                var newLeafNode = new LeafNode(HashProvider);
                newLeafNode.Value = currentNode.Value;
                newLeafNode.Nibbles = currentNode.Nibbles.SliceFrom(foundSameNibbles.Length + 1);
                branchNode.SetChild(currentNode.Nibbles[foundSameNibbles.Length], newLeafNode);
            }

            if(foundSameNibbles.Length > 0)
            {
                var extendedNode = new ExtendedNode(HashProvider);
                extendedNode.Nibbles = foundSameNibbles;
                extendedNode.InnerNode = branchNode;
                return extendedNode;
            }

            return branchNode;
        }

        private LeafNode PutOnAnExistingEmptyNode(byte[] keyAsNibbles, byte[] value)
        {
            var newLeafNode = new LeafNode(HashProvider);
            newLeafNode.Nibbles = keyAsNibbles;
            newLeafNode.Value = value;
            return newLeafNode;
        }

        public void Put(byte[] key, byte[] value)
        {
            Root = Put(Root, key.ConvertToNibbles(), value, new byte[0]);
        }

        public void Delete(byte[] key)
        {
            Root = Delete(Root, key.ConvertToNibbles(), new byte[0]);
        }

        private Node Delete(Node node, byte[] keyAsNibbles, byte[] path)
        {
            if (node is null || node is EmptyNode)
            {
                return EmptyNode.Instance;
            }

            if (node is LeafNode leafNode)
            {
                return DeleteFromLeafNode(leafNode, keyAsNibbles, path);
            }

            if (node is BranchNode branchNode)
            {
                return DeleteFromBranchNode(branchNode, keyAsNibbles, path);
            }

            if (node is ExtendedNode extendedNode)
            {
                return DeleteFromExtendedNode(extendedNode, keyAsNibbles, path);
            }

            if (node is HashNode hashNode)
            {
                return DeleteFromHashNode(hashNode, keyAsNibbles, path);
            }

            return node;
        }

        private Node DeleteFromHashNode(HashNode hashNode, byte[] keyAsNibbles, byte[] path)
        {
            ReloadInner(hashNode);
            hashNode.InnerNode = Delete(hashNode.InnerNode, keyAsNibbles, path);
            return NormalizeNode(hashNode.InnerNode);
        }

        private Node DeleteFromLeafNode(LeafNode leafNode, byte[] keyAsNibbles, byte[] path)
        {
            var foundSameNibbles = leafNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (foundSameNibbles.Length == leafNode.Nibbles.Length && foundSameNibbles.Length == keyAsNibbles.Length)
            {
                if (Tracer != null && !leafNode.NeedsPersist)
                    Tracer.OnRemove(_owner, path, leafNode.GetEncodedData());
                return EmptyNode.Instance;
            }
            return leafNode;
        }

        private Node DeleteFromBranchNode(BranchNode branchNode, byte[] keyAsNibbles, byte[] path)
        {
            var prevBlob = (Tracer != null && !branchNode.NeedsPersist) ? branchNode.GetEncodedData() : null;
            if (keyAsNibbles == null || keyAsNibbles.Length == 0)
            {
                branchNode.Value = null;
            }
            else
            {
                var nibble = keyAsNibbles[0];
                var newChild = Delete(branchNode.Children[nibble], keyAsNibbles.SliceFrom(1), ChildPath(path, new byte[] { nibble }));
                branchNode.SetChild(nibble, newChild);
            }
            var result = NormalizeBranchNode(branchNode, path);
            if (prevBlob != null) Tracer.OnRemove(_owner, path, prevBlob);
            return result;
        }

        private Node DeleteFromExtendedNode(ExtendedNode extendedNode, byte[] keyAsNibbles, byte[] path)
        {
            var foundSameNibbles = extendedNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (foundSameNibbles.Length < extendedNode.Nibbles.Length)
            {
                return extendedNode;
            }
            var prevBlob = (Tracer != null && !extendedNode.NeedsPersist) ? extendedNode.GetEncodedData() : null;
            extendedNode.InnerNode = Delete(extendedNode.InnerNode, keyAsNibbles.SliceFrom(foundSameNibbles.Length), ChildPath(path, extendedNode.Nibbles));
            var result = NormalizeExtendedNode(extendedNode, path);
            if (prevBlob != null) Tracer.OnRemove(_owner, path, prevBlob);
            return result;
        }

        private Node NormalizeNode(Node node)
        {
            if (node is EmptyNode) return EmptyNode.Instance;
            return node;
        }

        private Node NormalizeBranchNode(BranchNode branchNode, byte[] path)
        {
            int nonEmptyChildCount = 0;
            int nonEmptyChildIndex = -1;
            for (int i = 0; i < 16; i++)
            {
                if (branchNode.Children[i] != null && !(branchNode.Children[i] is EmptyNode))
                {
                    nonEmptyChildCount++;
                    nonEmptyChildIndex = i;
                }
            }

            bool hasNoValue = branchNode.Value == null || branchNode.Value.Length == 0;

            if (nonEmptyChildCount == 0 && hasNoValue)
            {
                return EmptyNode.Instance;
            }

            if (nonEmptyChildCount == 0 && !hasNoValue)
            {
                var leaf = new LeafNode(HashProvider);
                leaf.Nibbles = new byte[0];
                leaf.Value = branchNode.Value;
                return leaf;
            }

            if (nonEmptyChildCount == 1 && hasNoValue)
            {
                var child = branchNode.Children[nonEmptyChildIndex];
                var nibblePrefix = new byte[] { (byte)nonEmptyChildIndex };

                if (child is HashNode collapseHash)
                {
                    ReloadInner(collapseHash);
                    if (collapseHash.InnerNode != null) child = collapseHash.InnerNode;
                }

                if (child is LeafNode leafChild)
                {
                    if (Tracer != null && !leafChild.NeedsPersist)
                        Tracer.OnRemove(_owner, ChildPath(path, nibblePrefix), leafChild.GetEncodedData());
                    var newLeaf = new LeafNode(HashProvider);
                    newLeaf.Nibbles = nibblePrefix.ConcatArrays(leafChild.Nibbles);
                    newLeaf.Value = leafChild.Value;
                    return newLeaf;
                }
                else if (child is ExtendedNode extChild)
                {
                    if (Tracer != null && !extChild.NeedsPersist)
                        Tracer.OnRemove(_owner, ChildPath(path, nibblePrefix), extChild.GetEncodedData());
                    var newExt = new ExtendedNode(HashProvider);
                    newExt.Nibbles = nibblePrefix.ConcatArrays(extChild.Nibbles);
                    newExt.InnerNode = extChild.InnerNode;
                    return newExt;
                }
                else
                {
                    var newExt = new ExtendedNode(HashProvider);
                    newExt.Nibbles = nibblePrefix;
                    newExt.InnerNode = child;
                    return newExt;
                }
            }

            return branchNode;
        }

        private Node NormalizeExtendedNode(ExtendedNode extendedNode, byte[] path)
        {
            if (extendedNode.InnerNode is EmptyNode || extendedNode.InnerNode == null)
            {
                return EmptyNode.Instance;
            }

            if (extendedNode.InnerNode is HashNode innerHash)
            {
                ReloadInner(innerHash);
                if (innerHash.InnerNode != null) extendedNode.InnerNode = innerHash.InnerNode;
            }

            if (extendedNode.InnerNode is LeafNode leafChild)
            {
                if (Tracer != null && !leafChild.NeedsPersist)
                    Tracer.OnRemove(_owner, ChildPath(path, extendedNode.Nibbles), leafChild.GetEncodedData());
                var newLeaf = new LeafNode(HashProvider);
                newLeaf.Nibbles = extendedNode.Nibbles.ConcatArrays(leafChild.Nibbles);
                newLeaf.Value = leafChild.Value;
                return newLeaf;
            }

            if (extendedNode.InnerNode is ExtendedNode extChild)
            {
                if (Tracer != null && !extChild.NeedsPersist)
                    Tracer.OnRemove(_owner, ChildPath(path, extendedNode.Nibbles), extChild.GetEncodedData());
                var newExt = new ExtendedNode(HashProvider);
                newExt.Nibbles = extendedNode.Nibbles.ConcatArrays(extChild.Nibbles);
                newExt.InnerNode = extChild.InnerNode;
                return newExt;
            }

            return extendedNode;
        }


        public void SaveNodesToStorage()
        {
            if (_store == null) return;
            var collected = new System.Collections.Generic.List<Node>();
            var set = new TrieNodeSet();
            CollectNodes(Root, _owner, new byte[0], set, dirtyOnly: false, collected);
            FoldRemovalsIntoDeletes(set, commitTracer: false);
            _store.Commit(set);
            foreach (var n in collected) n.ClearNeedsPersist();
            Tracer?.Reset();
        }

        public void SaveDirtyNodesToStorage()
        {
            if (_store == null) return;
            var collected = new System.Collections.Generic.List<Node>();
            var set = new TrieNodeSet();
            CollectNodes(Root, _owner, new byte[0], set, dirtyOnly: true, collected);
            FoldRemovalsIntoDeletes(set, commitTracer: false);
            _store.Commit(set);
            foreach (var n in collected) n.ClearNeedsPersist();
            Tracer?.Reset();
        }

        private void FoldRemovalsIntoDeletes(TrieNodeSet set, bool commitTracer = true)
        {
            if (Tracer == null) return;
            var inserted = new HashSet<byte[]>(new ByteArrayComparer());
            foreach (var n in set.Nodes)
            {
                var rlp = n.GetEncodedData();
                if (rlp != null && rlp.Length >= 32)
                    inserted.Add(TrieTracer.Key(n.Owner, n.Path));
            }
            foreach (var d in Tracer.Removals)
            {
                if (!inserted.Contains(TrieTracer.Key(d.Owner, d.Path)))
                    set.AddDelete(d.Owner, d.Path, d.PrevBlob);
            }
            if (commitTracer) Tracer.Reset();
        }

        public void SaveDirtyNodesToStorageAndCollapse()
        {
            if (_store == null) return;
            SaveDirtyNodesToStorage();
            CollapseChildrenToHash(Root);
        }

        private void CollectNodes(Node node, byte[] owner, byte[] path, TrieNodeSet set, bool dirtyOnly,
            System.Collections.Generic.List<Node> collected = null)
        {
            if (node == null || node is EmptyNode) return;

            if (node is HashNode hashNode)
            {
                if (hashNode.InnerNode != null && (!dirtyOnly || hashNode.InnerNode.NeedsPersist))
                    CollectNodes(hashNode.InnerNode, owner, path, set, dirtyOnly, collected);
                return;
            }

            if (dirtyOnly && !node.NeedsPersist) return;

            node.Owner = owner;
            node.Path = path;
            set.Add(node);
            if (collected != null) collected.Add(node); else node.ClearNeedsPersist();

            if (node is BranchNode branchNode)
            {
                for (int i = 0; i < branchNode.Children.Length && i < 16; i++)
                    CollectNodes(branchNode.Children[i], owner, path.ConcatArrays(new byte[] { (byte)i }), set, dirtyOnly, collected);
            }
            else if (node is ExtendedNode extendedNode)
            {
                CollectNodes(extendedNode.InnerNode, owner, path.ConcatArrays(extendedNode.Nibbles), set, dirtyOnly, collected);
            }
        }

        private static void CollapseChildrenToHash(Node node)
        {
            if (node is BranchNode branch)
            {
                for (int i = 0; i < branch.Children.Length; i++)
                    branch.CollapseChild(i);
            }
            else if (node is ExtendedNode ext)
            {
                ext.CollapseInner();
            }
            else if (node is HashNode hashNode)
            {
                if (hashNode.InnerNode == null) return;
                if (hashNode.InnerNode.Path == null) return;
                var hash = hashNode.GetHash();
                hashNode.ReleaseInnerNode();
                hashNode.Hash = hash;
            }
        }
    }
}
