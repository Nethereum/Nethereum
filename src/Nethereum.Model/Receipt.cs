using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Receipt
    {
        public byte[] PostStateOrStatus { get; set; }
        public EvmUInt256 CumulativeGasUsed { get; set; }
        public byte[] Bloom { get; set; }
        public List<Log> Logs { get; set; } = new List<Log>();
        public byte TransactionType { get; set; } = 0;

        // --- EIP-6466 extensions (SSZ receipts) ---
        // These fields are ignored by the RLP encoder for backward compatibility.
        // The SSZ receipt encoder uses them to populate BasicReceipt / CreateReceipt /
        // SetCodeReceipt per the variant-selection rules:
        //   Authorities != null (and non-empty) → SetCodeReceipt
        //   ContractAddress != null             → CreateReceipt
        //   otherwise                           → BasicReceipt
        // Producers populate these at transaction-execution time. See
        // docs/superpowers/plans/2026-04-20-appchain-config-surface-A-plan.md.

        /// <summary>
        /// Transaction sender address (EIP-6466). Hex string (e.g. "0xabc…").
        /// Null for receipts produced before SSZ receipt support landed in the producer.
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Deployment address for contract-creation transactions (EIP-6466).
        /// Hex string. Null for non-creation transactions.
        /// </summary>
        public string ContractAddress { get; set; }

        /// <summary>
        /// EIP-7702 authorization addresses applied by this transaction. Null or
        /// empty for non-SetCode transactions.
        /// </summary>
        public List<string> Authorities { get; set; }

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

        public static Receipt CreateStatusReceipt(bool success, EvmUInt256 cumulativeGasUsed, byte[] bloom, List<Log> logs)
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

        public static Receipt CreatePostStateReceipt(byte[] postStateRoot, EvmUInt256 cumulativeGasUsed, byte[] bloom, List<Log> logs)
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
