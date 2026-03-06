using System;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageResult
    {
        public ulong SourceChainId { get; set; }
        public ulong MessageId { get; set; }
        public int LeafIndex { get; set; }
        public byte[] TxHash { get; set; } = Array.Empty<byte>();
        public bool Success { get; set; }
        public byte[] DataHash { get; set; } = Array.Empty<byte>();

        public MessageLeaf ToLeaf()
        {
            return new MessageLeaf
            {
                SourceChainId = SourceChainId,
                MessageId = MessageId,
                AppChainTxHash = TxHash,
                Success = Success,
                DataHash = DataHash
            };
        }
    }
}
