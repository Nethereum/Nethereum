using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Model
{
    public class Receipt
    {
        public byte[] PostStateOrStatus { get; set; }
        public BigInteger CumulativeGasUsed { get; set; }
        public byte[] Bloom { get; set; }
        public List<Log> Logs { get; set; } = new List<Log>();
        public byte TransactionType { get; set; } = 0;

        public bool IsStatusReceipt => PostStateOrStatus != null && PostStateOrStatus.Length <= 1;

        public bool? HasSucceeded
        {
            get
            {
                if (!IsStatusReceipt) return null;
                // Empty array (length 0) = failure, array with [1] = success
                return PostStateOrStatus.Length == 1 && PostStateOrStatus[0] == 1;
            }
        }

        public static Receipt CreateStatusReceipt(bool success, BigInteger cumulativeGasUsed, byte[] bloom, List<Log> logs)
        {
            return new Receipt
            {
                // In Ethereum, status 1 (success) is encoded as [0x01], status 0 (failure) as empty []
                // This matches RLP encoding: empty array → 0x80, [1] → 0x01
                PostStateOrStatus = success ? new byte[] { 1 } : new byte[0],
                CumulativeGasUsed = cumulativeGasUsed,
                Bloom = bloom,
                Logs = logs ?? new List<Log>()
            };
        }

        public static Receipt CreatePostStateReceipt(byte[] postStateRoot, BigInteger cumulativeGasUsed, byte[] bloom, List<Log> logs)
        {
            return new Receipt
            {
                PostStateOrStatus = postStateRoot,
                CumulativeGasUsed = cumulativeGasUsed,
                Bloom = bloom,
                Logs = logs ?? new List<Log>()
            };
        }
    }
}
