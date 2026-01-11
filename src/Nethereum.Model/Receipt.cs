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

        public bool IsStatusReceipt => PostStateOrStatus != null && PostStateOrStatus.Length == 1;

        public bool? HasSucceeded
        {
            get
            {
                if (!IsStatusReceipt) return null;
                return PostStateOrStatus[0] == 1;
            }
        }

        public static Receipt CreateStatusReceipt(bool success, BigInteger cumulativeGasUsed, byte[] bloom, List<Log> logs)
        {
            return new Receipt
            {
                PostStateOrStatus = new byte[] { success ? (byte)1 : (byte)0 },
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
