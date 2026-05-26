using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nethereum.CoreChain.DataAvailability
{
    public class AnchorReceiptRecorder
    {
        private readonly ConcurrentDictionary<long, AnchorReceipt> _receipts = new();

        public void Record(long blockNumber, byte[] anchorTxHash, byte[] encodedPayload)
        {
            var receipt = new AnchorReceipt
            {
                BlockNumber = blockNumber,
                AnchorTxHash = anchorTxHash,
                EncodedPayloadLength = encodedPayload?.Length ?? 0
            };

            if (encodedPayload != null)
            {
                var payload = AnchorPayloadCodec.Decode(encodedPayload);
                int offset = AnchorPayloadHeader.HeaderSize;

                foreach (var section in payload.Sections)
                {
                    var dataLength = section.Bytes?.Length ?? 0;
                    receipt.SectionOffsets[section.Type] = new CalldataCommitment
                    {
                        AnchorTxHash = anchorTxHash,
                        Offset = offset + AnchorPayloadCodec.SectionHeaderSize,
                        Length = dataLength,
                        ContentHash = null
                    };
                    offset += AnchorPayloadCodec.SectionHeaderSize + dataLength;
                }
            }

            _receipts[blockNumber] = receipt;
        }

        public AnchorReceipt GetReceipt(long blockNumber)
        {
            _receipts.TryGetValue(blockNumber, out var receipt);
            return receipt;
        }
    }

    public class AnchorReceipt
    {
        public long BlockNumber { get; init; }
        public byte[] AnchorTxHash { get; init; }
        public int EncodedPayloadLength { get; init; }
        public Dictionary<AnchorPayloadSectionType, CalldataCommitment> SectionOffsets { get; } = new();
    }
}
