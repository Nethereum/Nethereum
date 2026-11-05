using System.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    internal readonly struct HeaderCommitmentComparison
    {
        public HeaderCommitmentComparison(
            bool receiptsRootMismatch,
            bool logsBloomMismatch,
            bool gasUsedMismatch,
            bool blobGasUsedMismatch,
            bool blockAccessListHashMismatch)
        {
            ReceiptsRootMismatch = receiptsRootMismatch;
            LogsBloomMismatch = logsBloomMismatch;
            GasUsedMismatch = gasUsedMismatch;
            BlobGasUsedMismatch = blobGasUsedMismatch;
            BlockAccessListHashMismatch = blockAccessListHashMismatch;
        }

        public bool ReceiptsRootMismatch { get; }
        public bool LogsBloomMismatch { get; }
        public bool GasUsedMismatch { get; }
        public bool BlobGasUsedMismatch { get; }

        public bool BlockAccessListHashMismatch { get; }
    }

    internal static class HeaderCommitments
    {
        internal static HeaderCommitmentComparison CompareHeaderCommitments(
            Nethereum.Model.IBlockRootsProvider blockRootsProvider,
            BlockHeader header,
            List<TransactionExecutionResult> receipts,
            byte[]? blockBloom,
            Nethereum.EVM.Gas.BlockGasCapacity capacity,
            List<AccountChanges>? blockAccessList,
            bool carriesBlockAccessList)
        {
            var computedReceiptsRoot = blockRootsProvider.CalculateReceiptsRoot(TrieMemberReceipts(receipts));

            var computedBlobGasUsed = (long)capacity.BlobCount * BlobGasCalculator.GAS_PER_BLOB;

            var computedBlockAccessListHash = carriesBlockAccessList && blockAccessList != null
                ? BlockAccessListRLPEncoder.Current.Hash(blockAccessList)
                : null;

            return new HeaderCommitmentComparison(
                receiptsRootMismatch: header.ReceiptHash != null
                    && !ByteUtil.AreEqual(computedReceiptsRoot, header.ReceiptHash),
                logsBloomMismatch: header.LogsBloom != null
                    && !ByteUtil.AreEqual(blockBloom ?? new byte[256], header.LogsBloom),
                gasUsedMismatch: capacity.HeaderGasUsed != header.GasUsed,
                blobGasUsedMismatch: header.BlobGasUsed is long declaredBlobGasUsed
                    && computedBlobGasUsed != declaredBlobGasUsed,
                blockAccessListHashMismatch: carriesBlockAccessList
                    && (computedBlockAccessListHash == null
                        || header.BlockAccessListHash == null
                        || !ByteUtil.AreEqual(computedBlockAccessListHash, header.BlockAccessListHash)));
        }

        private static List<Receipt> TrieMemberReceipts(List<TransactionExecutionResult> receipts)
        {
            var members = new List<Receipt>(receipts.Count);
            foreach (var executed in receipts)
            {
                if (executed.Skipped) continue;
                if (executed.Receipt != null) members.Add(executed.Receipt);
            }
            return members;
        }

        internal static bool RequestsHashDoesNotMatchTheHeader(BlockHeader header, byte[]? computed)
        {
            if (computed == null || header.RequestsHash == null) return false;
            if (header.RequestsHash.Length != computed.Length) return true;

            for (var i = 0; i < computed.Length; i++)
                if (header.RequestsHash[i] != computed[i])
                    return true;

            return false;
        }
    }
}
