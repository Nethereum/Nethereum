using System;
using Nethereum.Documentation;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Merkle.Patricia
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "stack-trie", "Hash a sorted key stream without holding the trie")]
    public sealed class StackTrie
    {
        private const int KeyBytes = 32;
        private const int KeyNibbles = KeyBytes * 2;
        private const int HashBytes = 32;
        private const byte EmptyRlp = 0x80;

        private enum Kind : byte { Empty, Branch, Extension, Leaf, Collapsed }

        private sealed class StNode
        {
            public Kind Kind;
            public byte[] Key;
            public byte[] Value;
            public StNode[] Children;
        }

        private readonly int _prefixLength;
        private readonly NodeEmitted _onNode;
        private readonly byte[] _path = new byte[KeyNibbles];
        private readonly byte[] _lastKey = new byte[KeyBytes];
        private StNode _root = new StNode();
        private bool _hasKeys;
        private byte[] _rootBlob;
        private byte[] _rootHash;

        public StackTrie(byte[] prefixNibbles = null, NodeEmitted onNode = null)
        {
            var prefix = prefixNibbles ?? new byte[0];
            if (prefix.Length > KeyNibbles)
                throw new ArgumentOutOfRangeException(nameof(prefixNibbles), "The prefix must be at most 64 nibbles.");
            for (int i = 0; i < prefix.Length; i++)
            {
                if (prefix[i] > 0x0F)
                    throw new ArgumentOutOfRangeException(nameof(prefixNibbles), "Every prefix nibble must be between 0 and 15.");
            }
            Buffer.BlockCopy(prefix, 0, _path, 0, prefix.Length);
            _prefixLength = prefix.Length;
            _onNode = onNode;
        }

        public void Update(byte[] key, byte[] value)
        {
            if (_rootBlob != null) throw new InvalidOperationException("The stack trie has already been hashed.");
            if (key == null || key.Length != KeyBytes) throw new ArgumentException("A stack trie key must be 32 bytes.", nameof(key));
            if (value == null || value.Length == 0) throw new ArgumentException("A stack trie value must not be empty.", nameof(value));
            if (_hasKeys && ByteArrayComparer.Current.Compare(_lastKey, key) >= 0) throw new ArgumentException("Stack trie keys must be strictly ascending.", nameof(key));
            if (!StartsWithPrefix(key)) throw new ArgumentException("Every stack trie key must start with the prefix nibbles.", nameof(key));

            _hasKeys = true;
            Buffer.BlockCopy(key, 0, _lastKey, 0, KeyBytes);
            var nibbles = new byte[KeyNibbles - _prefixLength];
            for (int i = 0; i < nibbles.Length; i++) nibbles[i] = NibbleAt(key, _prefixLength + i);
            Insert(_root, nibbles, 0, (byte[])value.Clone(), _prefixLength);
        }

        public byte[] Reference()
        {
            Seal();
            if (!_hasKeys) return (byte[])DefaultValues.EMPTY_TRIE_HASH.Clone();
            return _rootBlob.Length >= HashBytes ? (byte[])_rootHash.Clone() : (byte[])_rootBlob.Clone();
        }

        public byte[] Hash()
        {
            Seal();
            return (byte[])_rootHash.Clone();
        }

        private void Seal()
        {
            if (_rootBlob != null) return;
            if (!_hasKeys)
            {
                _rootBlob = new[] { EmptyRlp };
                _rootHash = (byte[])DefaultValues.EMPTY_TRIE_HASH.Clone();
                return;
            }
            CollapseChildren(_root, _prefixLength);
            _rootBlob = Encode(_root);
            _rootHash = Sha3Keccack.Current.CalculateHash(_rootBlob);
            if (_rootBlob.Length >= HashBytes) Emit(_prefixLength, _rootBlob);
            _root = null;
        }

        private void Insert(StNode node, byte[] key, int offset, byte[] value, int pathLength)
        {
            switch (node.Kind)
            {
                case Kind.Branch:
                    InsertIntoBranch(node, key, offset, value, pathLength);
                    return;
                case Kind.Extension:
                    InsertIntoExtension(node, key, offset, value, pathLength);
                    return;
                case Kind.Leaf:
                    SplitLeaf(node, key, offset, value, pathLength);
                    return;
                case Kind.Empty:
                    node.Kind = Kind.Leaf;
                    node.Key = Slice(key, offset, key.Length - offset);
                    node.Value = value;
                    return;
                default:
                    throw new InvalidOperationException("Cannot insert below a node that has already been collapsed.");
            }
        }

        private void InsertIntoBranch(StNode node, byte[] key, int offset, byte[] value, int pathLength)
        {
            var index = key[offset];
            CollapseCompletedElderSibling(node, index, pathLength);

            var child = node.Children[index];
            if (child == null)
            {
                node.Children[index] = NewLeaf(key, offset + 1, value);
                return;
            }
            _path[pathLength] = index;
            Insert(child, key, offset + 1, value, pathLength + 1);
        }

        private void CollapseCompletedElderSibling(StNode branch, int index, int pathLength)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                var elder = branch.Children[i];
                if (elder == null) continue;
                _path[pathLength] = (byte)i;
                Collapse(elder, pathLength + 1);
                return;
            }
        }

        private void InsertIntoExtension(StNode node, byte[] key, int offset, byte[] value, int pathLength)
        {
            var diff = DiffIndex(node.Key, key, offset);
            if (diff == node.Key.Length)
            {
                WritePath(pathLength, node.Key, diff);
                Insert(node.Children[0], key, offset + diff, value, pathLength + diff);
                return;
            }

            StNode original;
            if (diff < node.Key.Length - 1)
            {
                original = new StNode { Kind = Kind.Extension, Key = Slice(node.Key, diff + 1, node.Key.Length - diff - 1), Children = new[] { node.Children[0] } };
                WritePath(pathLength, node.Key, diff + 1);
                Collapse(original, pathLength + diff + 1);
            }
            else
            {
                original = node.Children[0];
                WritePath(pathLength, node.Key, node.Key.Length);
                Collapse(original, pathLength + node.Key.Length);
            }

            var branch = BecomeBranchAt(node, diff);
            branch.Children[node.Key[diff]] = original;
            branch.Children[key[offset + diff]] = NewLeaf(key, offset + diff + 1, value);
            node.Key = Slice(node.Key, 0, diff);
        }

        private void SplitLeaf(StNode node, byte[] key, int offset, byte[] value, int pathLength)
        {
            var diff = DiffIndex(node.Key, key, offset);
            if (diff >= node.Key.Length) throw new InvalidOperationException("The stack trie cannot overwrite an existing key.");

            var originalIndex = node.Key[diff];
            var original = NewLeaf(node.Key, diff + 1, node.Value);
            var branch = BecomeBranchAt(node, diff);
            branch.Children[originalIndex] = original;
            WritePath(pathLength, node.Key, diff + 1);
            Collapse(original, pathLength + diff + 1);
            branch.Children[key[offset + diff]] = NewLeaf(key, offset + diff + 1, value);
            node.Key = Slice(node.Key, 0, diff);
            node.Value = null;
        }

        private static StNode BecomeBranchAt(StNode node, int diff)
        {
            if (diff == 0)
            {
                node.Kind = Kind.Branch;
                node.Children = new StNode[16];
                return node;
            }
            var branch = new StNode { Kind = Kind.Branch, Children = new StNode[16] };
            node.Kind = Kind.Extension;
            node.Children = new[] { branch };
            return branch;
        }

        private void Collapse(StNode node, int pathLength)
        {
            if (node.Kind == Kind.Collapsed) return;
            CollapseChildren(node, pathLength);
            var blob = Encode(node);
            node.Kind = Kind.Collapsed;
            node.Key = null;
            node.Children = null;
            if (blob.Length < HashBytes)
            {
                node.Value = blob;
                return;
            }
            node.Value = Sha3Keccack.Current.CalculateHash(blob);
            Emit(pathLength, blob);
        }

        private void CollapseChildren(StNode node, int pathLength)
        {
            if (node.Kind == Kind.Branch)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (node.Children[i] == null) continue;
                    _path[pathLength] = (byte)i;
                    Collapse(node.Children[i], pathLength + 1);
                }
            }
            else if (node.Kind == Kind.Extension)
            {
                WritePath(pathLength, node.Key, node.Key.Length);
                Collapse(node.Children[0], pathLength + node.Key.Length);
            }
        }

        private static byte[] Encode(StNode node)
        {
            switch (node.Kind)
            {
                case Kind.Branch:
                    var items = new byte[17][];
                    for (int i = 0; i < 16; i++)
                        items[i] = node.Children[i] == null ? new[] { EmptyRlp } : ChildReference(node.Children[i]);
                    items[16] = new[] { EmptyRlp };
                    return RLP.RLP.EncodeList(items);
                case Kind.Extension:
                    return RLP.RLP.EncodeList(RLP.RLP.EncodeElement(PatriciaPathWalker.NibblesToCompact(node.Key, false)), ChildReference(node.Children[0]));
                case Kind.Leaf:
                    return RLP.RLP.EncodeList(RLP.RLP.EncodeElement(PatriciaPathWalker.NibblesToCompact(node.Key, true)), RLP.RLP.EncodeElement(node.Value));
                default:
                    throw new InvalidOperationException("Only branch, extension and leaf nodes are encoded.");
            }
        }

        private static byte[] ChildReference(StNode collapsed)
            => collapsed.Value.Length == HashBytes ? RLP.RLP.EncodeElement(collapsed.Value) : collapsed.Value;

        private void Emit(int pathLength, byte[] blob)
        {
            if (_onNode == null) return;
            var path = new byte[pathLength];
            Buffer.BlockCopy(_path, 0, path, 0, pathLength);
            _onNode(path, blob);
        }

        private void WritePath(int pathLength, byte[] nibbles, int count)
            => Buffer.BlockCopy(nibbles, 0, _path, pathLength, count);

        private static StNode NewLeaf(byte[] key, int offset, byte[] value)
            => new StNode { Kind = Kind.Leaf, Key = Slice(key, offset, key.Length - offset), Value = value };

        private static int DiffIndex(byte[] nodeKey, byte[] key, int offset)
        {
            for (int i = 0; i < nodeKey.Length; i++)
            {
                if (nodeKey[i] != key[offset + i]) return i;
            }
            return nodeKey.Length;
        }

        private bool StartsWithPrefix(byte[] key)
        {
            for (int i = 0; i < _prefixLength; i++)
            {
                if (NibbleAt(key, i) != _path[i]) return false;
            }
            return true;
        }

        private static byte NibbleAt(byte[] key, int nibbleIndex)
        {
            var b = key[nibbleIndex >> 1];
            return (byte)((nibbleIndex & 1) == 0 ? b >> 4 : b & 0x0F);
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            var slice = new byte[length];
            Buffer.BlockCopy(source, offset, slice, 0, length);
            return slice;
        }
    }
}
