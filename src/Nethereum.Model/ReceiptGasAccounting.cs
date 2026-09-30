using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class NonMonotonicCumulativeGasException : Exception
    {
        public NonMonotonicCumulativeGasException(
            EvmUInt256 blockNumber,
            int receiptIndex,
            EvmUInt256 previousCumulativeGasUsed,
            EvmUInt256 cumulativeGasUsed)
            : base($"Block {blockNumber} receipt {receiptIndex}: cumulative gas {cumulativeGasUsed} is below " +
                   $"receipt {receiptIndex - 1}'s {previousCumulativeGasUsed}")
        {
            BlockNumber = blockNumber;
            ReceiptIndex = receiptIndex;
            PreviousCumulativeGasUsed = previousCumulativeGasUsed;
            CumulativeGasUsed = cumulativeGasUsed;
        }

        public EvmUInt256 BlockNumber { get; }
        public int ReceiptIndex { get; }
        public EvmUInt256 PreviousCumulativeGasUsed { get; }
        public EvmUInt256 CumulativeGasUsed { get; }
    }

    public static class ReceiptGasAccounting
    {
        public static EvmUInt256[] PerTransactionGasUsed(IReadOnlyList<Receipt> receipts, EvmUInt256 blockNumber)
        {
            var offending = FirstNonMonotonicIndex(receipts);
            if (offending >= 0)
            {
                throw new NonMonotonicCumulativeGasException(
                    blockNumber,
                    offending,
                    receipts[offending - 1].CumulativeGasUsed,
                    receipts[offending].CumulativeGasUsed);
            }

            if (receipts.Count == 0) return EmptyGas;

            var gasUsed = new EvmUInt256[receipts.Count];
            var previousCumulative = EvmUInt256.Zero;
            for (int i = 0; i < receipts.Count; i++)
            {
                var cumulative = receipts[i].CumulativeGasUsed;
                gasUsed[i] = cumulative - previousCumulative;
                previousCumulative = cumulative;
            }

            return gasUsed;
        }

        private static readonly EvmUInt256[] EmptyGas = new EvmUInt256[0];

        private static int FirstNonMonotonicIndex(IReadOnlyList<Receipt> receipts)
        {
            if (receipts == null) throw new ArgumentNullException(nameof(receipts));

            var previousCumulative = EvmUInt256.Zero;
            for (int i = 0; i < receipts.Count; i++)
            {
                var cumulative = receipts[i].CumulativeGasUsed;
                if (cumulative < previousCumulative) return i;
                previousCumulative = cumulative;
            }

            return -1;
        }
    }
}
