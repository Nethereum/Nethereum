using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageMerkleAccumulator : IMessageMerkleAccumulator
    {
        private readonly ConcurrentDictionary<ulong, LeanIncrementalMerkleTree<byte[]>> _chainTrees = new();
        private readonly ConcurrentDictionary<ulong, ulong> _lastProcessedIds = new();
        private readonly ConcurrentDictionary<ulong, object> _chainLocks = new();
        private readonly IHashProvider _hashProvider;
        private readonly IByteArrayConvertor<byte[]> _byteArrayConvertor;

        public MessageMerkleAccumulator()
        {
            _hashProvider = new Sha3KeccackHashProvider();
            _byteArrayConvertor = new ByteArrayToByteArrayConvertor();
        }

        public int AppendLeaf(ulong sourceChainId, MessageLeaf leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            if (leaf.AppChainTxHash == null || leaf.AppChainTxHash.Length == 0)
                throw new ArgumentException("Leaf must have a non-empty AppChainTxHash", nameof(leaf));

            var lockObj = _chainLocks.GetOrAdd(sourceChainId, _ => new object());

            lock (lockObj)
            {
                _lastProcessedIds.TryGetValue(sourceChainId, out var lastId);
                if (leaf.MessageId != lastId + 1 && lastId != 0)
                {
                    if (leaf.MessageId <= lastId)
                        throw new InvalidOperationException(
                            $"Duplicate or out-of-order message: chain={sourceChainId} messageId={leaf.MessageId} lastProcessed={lastId}");
                    throw new InvalidOperationException(
                        $"Non-contiguous message ID: chain={sourceChainId} messageId={leaf.MessageId} expected={lastId + 1}");
                }

                var tree = _chainTrees.GetOrAdd(sourceChainId, _ =>
                    new LeanIncrementalMerkleTree<byte[]>(
                        _hashProvider,
                        _byteArrayConvertor,
                        PairingConcatType.Sorted));

                int leafIndex = tree.Size;
                var encodedData = leaf.GetEncodedData();
                tree.InsertLeaf(encodedData);

                _lastProcessedIds[sourceChainId] = leaf.MessageId;

                return leafIndex;
            }
        }

        public byte[] GetRoot(ulong sourceChainId)
        {
            var lockObj = _chainLocks.GetOrAdd(sourceChainId, _ => new object());

            lock (lockObj)
            {
                if (_chainTrees.TryGetValue(sourceChainId, out var tree))
                    return tree.Root;
                return Array.Empty<byte>();
            }
        }

        public MerkleProof GenerateProof(ulong sourceChainId, int leafIndex)
        {
            var lockObj = _chainLocks.GetOrAdd(sourceChainId, _ => new object());

            lock (lockObj)
            {
                if (!_chainTrees.TryGetValue(sourceChainId, out var tree))
                    throw new InvalidOperationException($"No tree for source chain {sourceChainId}");
                if (leafIndex < 0 || leafIndex >= tree.Size)
                    throw new ArgumentOutOfRangeException(nameof(leafIndex),
                        $"Leaf index {leafIndex} is out of range. Tree has {tree.Size} leaves.");
                return tree.GenerateProof(leafIndex);
            }
        }

        public int GetLeafCount(ulong sourceChainId)
        {
            var lockObj = _chainLocks.GetOrAdd(sourceChainId, _ => new object());

            lock (lockObj)
            {
                if (_chainTrees.TryGetValue(sourceChainId, out var tree))
                    return tree.Size;
                return 0;
            }
        }

        public ulong GetLastProcessedMessageId(ulong sourceChainId)
        {
            return _lastProcessedIds.TryGetValue(sourceChainId, out var id) ? id : 0;
        }

        public (byte[] Root, ulong LastProcessedMessageId) GetSnapshot(ulong sourceChainId)
        {
            var lockObj = _chainLocks.GetOrAdd(sourceChainId, _ => new object());

            lock (lockObj)
            {
                var root = _chainTrees.TryGetValue(sourceChainId, out var tree)
                    ? tree.Root
                    : Array.Empty<byte>();
                var lastId = _lastProcessedIds.TryGetValue(sourceChainId, out var id) ? id : 0;
                return (root, lastId);
            }
        }

        public IReadOnlyList<ulong> GetSourceChainIds()
        {
            return _chainTrees.Keys.ToList();
        }
    }
}
