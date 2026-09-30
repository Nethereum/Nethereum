using System;
using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public sealed class BlockHeaderSealer
    {
        private readonly IBlockRootsProvider _blockRootsProvider;

        public BlockHeaderSealer(IBlockRootsProvider blockRootsProvider)
        {
            _blockRootsProvider = blockRootsProvider ?? throw new ArgumentNullException(nameof(blockRootsProvider));
        }

        public void SealHeader(
            BlockHeader header,
            IList<ISignedTransaction> includedTransactions,
            IList<Receipt> receipts,
            byte[] combinedBloom,
            BlockExecutionResult execResult,
            IList<Withdrawal>? withdrawals,
            byte[]? blockAccessListRlp)
        {
            StampExecutionRoots(header, includedTransactions, receipts, combinedBloom, execResult);
            StampWithdrawalsRoot(header, withdrawals);
            StampRequestsHash(header, execResult);
            StampBlockAccessListHash(header, blockAccessListRlp);
            BlockHeaderCodecs.ForFork(execResult.Fork).ClearFieldsNotCarried(header);
        }

        public void StampExecutionRoots(
            BlockHeader header,
            IList<ISignedTransaction> includedTransactions,
            IList<Receipt> receipts,
            byte[] combinedBloom,
            BlockExecutionResult execResult)
        {
            header.StateRoot = execResult.PostStateRoot;
            header.TransactionsHash = _blockRootsProvider.CalculateTransactionsRoot(includedTransactions);
            header.ReceiptHash = _blockRootsProvider.CalculateReceiptsRoot(receipts);
            header.LogsBloom = combinedBloom;
            header.GasUsed = (long)execResult.GasUsed;
        }

        public void StampWithdrawalsRoot(BlockHeader header, IList<Withdrawal>? withdrawals)
            => header.WithdrawalsRoot = _blockRootsProvider.CalculateWithdrawalsRoot(
                withdrawals ?? (IList<Withdrawal>)Array.Empty<Withdrawal>());

        public static void StampRequestsHash(BlockHeader header, BlockExecutionResult execResult)
            => header.RequestsHash = execResult.ComputedRequestsHash;

        public static void StampBlockAccessListHash(BlockHeader header, byte[]? blockAccessListRlp)
            => header.BlockAccessListHash = blockAccessListRlp != null
                ? new Sha3Keccack().CalculateHash(blockAccessListRlp)
                : null;
    }
}
