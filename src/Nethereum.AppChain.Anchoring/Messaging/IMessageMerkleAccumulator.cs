using System.Collections.Generic;
using Nethereum.Merkle;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageMerkleAccumulator
    {
        int AppendLeaf(ulong sourceChainId, MessageLeaf leaf);
        byte[] GetRoot(ulong sourceChainId);
        MerkleProof GenerateProof(ulong sourceChainId, int leafIndex);
        int GetLeafCount(ulong sourceChainId);
        ulong GetLastProcessedMessageId(ulong sourceChainId);
        IReadOnlyList<ulong> GetSourceChainIds();
        (byte[] Root, ulong LastProcessedMessageId) GetSnapshot(ulong sourceChainId);
    }
}
