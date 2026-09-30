using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM.Gas;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    internal sealed class BlockAccumulation
    {
        private readonly byte[] _combinedBloom = new byte[256];
        private bool _anyReceipt;

        public BlockAccumulation(int transactionCount)
        {
            Receipts = new List<TransactionExecutionResult>(transactionCount);
        }

        public List<TransactionExecutionResult> Receipts { get; }

        public List<Log> Logs { get; } = new List<Log>();

        public BlockGasCapacity Capacity { get; } = new BlockGasCapacity();

        public BigInteger CumulativeGasUsed { get; private set; }

        public bool ContainsInvalidTransaction { get; private set; }

        public byte[]? BlockBloom => _anyReceipt ? _combinedBloom : null;

        public void Add(
            ISignedTransaction transaction,
            TransactionExecutionResult result,
            bool refusalInvalidatesTheBlock,
            bool stateGasActive)
        {
            CumulativeGasUsed = result.CumulativeGasUsed;

            if (refusalInvalidatesTheBlock && result.RefusedByValidationRule)
                ContainsInvalidTransaction = true;

            Capacity.Add((long)result.GasUsed, result.ExecutionGasUsed, result.StateGasUsed,
                BlobsIncluded(transaction, result), stateGasActive);

            Receipts.Add(result);

            if (result.Receipt != null)
            {
                _anyReceipt = true;
                CombineBloom(_combinedBloom, result.Receipt.Bloom);
            }

            if (result.Logs != null && result.Logs.Count > 0) Logs.AddRange(result.Logs);
        }

        private static int BlobsIncluded(ISignedTransaction tx, TransactionExecutionResult result)
        {
            if (result.Skipped || !(tx is Transaction4844 blobTx) || blobTx.BlobVersionedHashes == null)
                return 0;

            return blobTx.BlobVersionedHashes.Count;
        }

        private static void CombineBloom(byte[] target, byte[]? source)
        {
            if (source == null || source.Length != 256) return;

            for (int i = 0; i < 256; i++) target[i] |= source[i];
        }
    }
}
