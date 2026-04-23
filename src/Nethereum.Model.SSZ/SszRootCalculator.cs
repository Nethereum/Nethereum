using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Model.SSZ
{
    /// <summary>
    /// EIP-7807 block-root calculator. Computes <c>transactionsRoot</c>,
    /// <c>receiptsRoot</c>, and <c>withdrawalsRoot</c> as
    /// <c>hash_tree_root(ProgressiveList[T])</c> over per-item SSZ roots, where
    /// each item root matches the progressive-merkleisation rules in its source
    /// EIP:
    ///   transactions  → EIP-6404 (CompatibleUnion wrapping 10 payload variants)
    ///   receipts      → EIP-6466 (CompatibleUnion wrapping Basic / Create / SetCode)
    ///   withdrawals   → EIP-6465 (ProgressiveContainer, 4 fields)
    /// ProgressiveList merkleisation is defined in EIP-7916 and reused by 7495.
    ///
    /// Consumers with already-computed per-item roots should call the
    /// <c>*FromRoots</c> overloads; the object-typed overloads are convenience
    /// wrappers that compute the per-item root first.
    /// </summary>
    public class SszRootCalculator
    {
        public static readonly SszRootCalculator Current = new SszRootCalculator();

        // --- transactions ---

        public byte[] CalculateTransactionsRoot(IList<ISignedTransaction> transactions)
        {
            var roots = new List<byte[]>(transactions?.Count ?? 0);
            if (transactions != null)
            {
                foreach (var tx in transactions)
                    roots.Add(HashTreeRootTransaction(tx));
            }
            return CalculateTransactionsRootFromRoots(roots);
        }

        public byte[] CalculateTransactionsRootFromRoots(IList<byte[]> transactionRoots)
            => SszMerkleizer.HashTreeRootProgressiveList(transactionRoots ?? new List<byte[]>());

        /// <summary>
        /// Dispatches <paramref name="tx"/> to the concrete-type
        /// <c>HashTreeRoot*</c> method on <see cref="SszTransactionEncoder"/>.
        /// Legacy / EIP-2930 / EIP-4844 blob transactions are not yet wired
        /// (SszTransactionEncoder exposes HashTreeRoot methods only for 1559 and
        /// 7702 today). Backlog item A-02a covers filling those variants in.
        /// </summary>
        public byte[] HashTreeRootTransaction(ISignedTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));

            switch (tx)
            {
                case Transaction7702 setCode:
                    return SszTransactionEncoder.Current.HashTreeRootTransaction7702(setCode);
                case Transaction1559 eip1559:
                    return SszTransactionEncoder.Current.HashTreeRootTransaction1559(eip1559);
                default:
                    throw new NotImplementedException(
                        $"SSZ hash_tree_root not yet implemented for {tx.GetType().Name} " +
                        $"(TransactionType={tx.TransactionType}). EIP-6404 defines 10 " +
                        "selectors (0x01-0x0a); currently only 1559 (0x07/0x08) and " +
                        "7702 (0x0a) are wired in SszTransactionEncoder. Add a " +
                        "HashTreeRootTransaction{Type} method there and extend this " +
                        "switch. See docs/superpowers/plans/2026-04-20-appchain-config-surface-A-plan.md backlog A-02a.");
            }
        }

        // --- receipts ---

        public byte[] CalculateReceiptsRoot(IList<Receipt> receipts)
        {
            var roots = new List<byte[]>(receipts?.Count ?? 0);
            if (receipts != null)
            {
                foreach (var r in receipts)
                    roots.Add(HashTreeRootReceipt(r));
            }
            return CalculateReceiptsRootFromRoots(roots);
        }

        public byte[] CalculateReceiptsRootFromRoots(IList<byte[]> receiptRoots)
            => SszMerkleizer.HashTreeRootProgressiveList(receiptRoots ?? new List<byte[]>());

        /// <summary>
        /// Computes <c>hash_tree_root(Receipt)</c> per EIP-6466. Variant is
        /// selected by the same Authorities / ContractAddress / else rule used
        /// by <see cref="SszBlockEncodingProvider.EncodeReceipt"/>. The producer
        /// must populate <see cref="Receipt.From"/> (and the appropriate variant
        /// field) at tx-execution time.
        /// </summary>
        public byte[] HashTreeRootReceipt(Receipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            if (string.IsNullOrEmpty(receipt.From))
                throw new InvalidOperationException(
                    "SSZ HashTreeRootReceipt: Receipt.From is required (EIP-6466). " +
                    "The block producer must populate it at tx-execution time.");

            var status = receipt.HasSucceeded == true;
            var gasUsed = (ulong)receipt.CumulativeGasUsed.ToLong();
            var encoder = SszReceiptEncoder.Current;

            byte[] variantRoot;
            byte selector;
            if (receipt.Authorities != null && receipt.Authorities.Count > 0)
            {
                variantRoot = encoder.HashTreeRootSetCodeReceipt(
                    receipt.From, gasUsed, receipt.Logs, status, receipt.Authorities);
                selector = SszReceiptEncoder.SelectorSetCodeReceipt;
            }
            else if (!string.IsNullOrEmpty(receipt.ContractAddress))
            {
                variantRoot = encoder.HashTreeRootCreateReceipt(
                    receipt.From, gasUsed, receipt.ContractAddress, receipt.Logs, status);
                selector = SszReceiptEncoder.SelectorCreateReceipt;
            }
            else
            {
                variantRoot = encoder.HashTreeRootBasicReceipt(
                    receipt.From, gasUsed, receipt.Logs, status);
                selector = SszReceiptEncoder.SelectorBasicReceipt;
            }

            return encoder.HashTreeRootReceipt(selector, variantRoot);
        }

        // --- withdrawals ---

        public byte[] CalculateWithdrawalsRoot(
            IList<(ulong Index, ulong ValidatorIndex, byte[] Address, ulong AmountInGwei)> withdrawals)
        {
            var roots = new List<byte[]>(withdrawals?.Count ?? 0);
            if (withdrawals != null)
            {
                foreach (var w in withdrawals)
                    roots.Add(SszWithdrawalEncoder.Current.HashTreeRoot(
                        w.Index, w.ValidatorIndex, w.Address, w.AmountInGwei));
            }
            return CalculateWithdrawalsRootFromRoots(roots);
        }

        public byte[] CalculateWithdrawalsRootFromRoots(IList<byte[]> withdrawalRoots)
            => SszMerkleizer.HashTreeRootProgressiveList(withdrawalRoots ?? new List<byte[]>());
    }
}
