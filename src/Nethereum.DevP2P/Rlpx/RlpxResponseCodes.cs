using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Rlpx
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p", "RlpxResponseCodes — which message ids are responses")]
    public static class RlpxResponseCodes
    {
        public static bool IsResponse(string capabilityName, int relativeMessageId)
        {
            if (capabilityName == "eth")
                return relativeMessageId == EthMessageIds.BlockHeaders
                    || relativeMessageId == EthMessageIds.BlockBodies
                    || relativeMessageId == EthMessageIds.PooledTransactions
                    || relativeMessageId == EthMessageIds.Receipts
                    || relativeMessageId == EthMessageIds.BlockAccessLists;
            if (capabilityName == "snap")
                return relativeMessageId == SnapMessageIds.AccountRange
                    || relativeMessageId == SnapMessageIds.StorageRanges
                    || relativeMessageId == SnapMessageIds.ByteCodes
                    || relativeMessageId == SnapMessageIds.TrieNodes
                    || relativeMessageId == SnapMessageIds.BlockAccessLists;
            return false;
        }
    }
}
