using System;
using System.Collections.Generic;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Merkle.Binary.Storage;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public class BinaryTrieStateDiffProducer
    {
        public static BinaryTrieStateDiff Produce(
            long blockNumber,
            byte[] preStateRoot,
            byte[] postStateRoot,
            IBinaryTrieNodeStore nodeStore)
        {
            if (nodeStore == null) throw new ArgumentNullException(nameof(nodeStore));

            var diff = new BinaryTrieStateDiff
            {
                BlockNumber = blockNumber,
                PreStateRoot = preStateRoot ?? new byte[32],
                PostStateRoot = postStateRoot ?? new byte[32]
            };

            var dirtyNodes = nodeStore.GetDirtyNodes();

            foreach (var node in dirtyNodes)
            {
                if (node.NodeType == BinaryTrieConstants.NodeTypeStem && node.Stem != null)
                {
                    var stemDiff = new StemDiff { Stem = node.Stem };
                    var decoded = CompactBinaryNodeCodec.Decode(node.Encoded, node.Depth);
                    if (decoded is StemBinaryNode stemNode)
                    {
                        for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
                        {
                            if (stemNode.Values[i] != null)
                            {
                                stemDiff.SuffixDiffs.Add(new SuffixDiff
                                {
                                    SuffixIndex = (byte)i,
                                    OldValue = null,
                                    NewValue = stemNode.Values[i]
                                });
                            }
                        }
                    }

                    if (stemDiff.SuffixDiffs.Count > 0)
                        diff.StemDiffs.Add(stemDiff);
                }
                else if (node.NodeType == BinaryTrieConstants.NodeTypeInternal)
                {
                    diff.ProofSiblings.Add(node.Hash);
                }
            }

            return diff;
        }
    }
}
