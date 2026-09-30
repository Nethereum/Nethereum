#nullable enable
using System;
using System.Collections.Generic;
using Nethereum.Documentation;

namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "BlockValidityCheck — the block-validity verdicts a followed block can fail")]
    public enum BlockValidityCheck
    {
        StateRoot,
        ReceiptsRoot,
        LogsBloom,
        GasUsed,
        GasCapacity,
        BlobGasCapacity,
        BlobGasUsed,
        ExcessBlobGas,
        BlobFieldFormat,
        BaseFee,
        BlockAccessListHash,
        BlockAccessListGasLimit,
        BlockAccessListMalformed,
        RequestsHash,
        InvalidTransaction,
        GasLimitBound,
        WithdrawalsRoot
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "IBlockValidityChecks — the per-check flags a block execution reports")]
    public interface IBlockValidityChecks
    {
        bool StateRootMismatch { get; }
        bool ReceiptsRootMismatch { get; }
        bool LogsBloomMismatch { get; }
        bool GasUsedMismatch { get; }
        bool GasCapacityExceeded { get; }
        bool BlobGasCapacityExceeded { get; }
        bool BlobGasUsedMismatch { get; }
        bool ExcessBlobGasMismatch { get; }
        bool BlobFieldFormatMismatch { get; }
        bool BaseFeeMismatch { get; }
        bool BlockAccessListHashMismatch { get; }

        bool BlockAccessListGasLimitExceeded { get; }

        /// <summary>
        /// AMS-7928-35. EIP-7928 §Engine API: "Returns <c>INVALID</c> if access list is
        /// malformed or doesn't match" — this is the first verdict and
        /// <see cref="BlockAccessListHashMismatch"/> the second. The canonical form it
        /// answers for lives in
        /// <see cref="Nethereum.EVM.Execution.BlockAccessListStructureRule"/>.
        /// </summary>
        bool BlockAccessListMalformed { get; }

        bool RequestsHashMismatch { get; }

        bool ContainsInvalidTransaction { get; }

        bool GasLimitBoundViolated { get; }

        bool WithdrawalsRootMismatch { get; }
    }

    public static class BlockValidityChecks
    {
        private static readonly (BlockValidityCheck Check, string Identifier, Func<IBlockValidityChecks, bool> Failed)[] All =
        {
            (BlockValidityCheck.StateRoot, "stateRoot", c => c.StateRootMismatch),
            (BlockValidityCheck.ReceiptsRoot, "receiptsRoot", c => c.ReceiptsRootMismatch),
            (BlockValidityCheck.LogsBloom, "logsBloom", c => c.LogsBloomMismatch),
            (BlockValidityCheck.GasUsed, "gasUsed", c => c.GasUsedMismatch),
            (BlockValidityCheck.GasCapacity, "gasCapacity", c => c.GasCapacityExceeded),
            (BlockValidityCheck.BlobGasCapacity, "blobGasCapacity", c => c.BlobGasCapacityExceeded),
            (BlockValidityCheck.BlobGasUsed, "blobGasUsed", c => c.BlobGasUsedMismatch),
            (BlockValidityCheck.ExcessBlobGas, "excessBlobGas", c => c.ExcessBlobGasMismatch),
            (BlockValidityCheck.BlobFieldFormat, "blobFieldFormat", c => c.BlobFieldFormatMismatch),
            (BlockValidityCheck.BaseFee, "baseFee", c => c.BaseFeeMismatch),
            (BlockValidityCheck.BlockAccessListHash, "blockAccessListHash", c => c.BlockAccessListHashMismatch),
            (BlockValidityCheck.BlockAccessListGasLimit, "blockAccessListGasLimit", c => c.BlockAccessListGasLimitExceeded),
            (BlockValidityCheck.BlockAccessListMalformed, "blockAccessListMalformed", c => c.BlockAccessListMalformed),
            (BlockValidityCheck.RequestsHash, "requestsHash", c => c.RequestsHashMismatch),
            (BlockValidityCheck.InvalidTransaction, "invalidTransaction", c => c.ContainsInvalidTransaction),
            (BlockValidityCheck.GasLimitBound, "gasLimitBound", c => c.GasLimitBoundViolated),
            (BlockValidityCheck.WithdrawalsRoot, "withdrawalsRoot", c => c.WithdrawalsRootMismatch)
        };

        public static string Identifier(this BlockValidityCheck check)
        {
            foreach (var entry in All)
                if (entry.Check == check) return entry.Identifier;
            throw new ArgumentOutOfRangeException(
                nameof(check), check, "No wire identifier is registered for this check.");
        }

        public static bool AnyFailed(this IBlockValidityChecks checks)
        {
            foreach (var entry in All)
                if (entry.Failed(checks)) return true;
            return false;
        }

        public static IReadOnlyList<BlockValidityCheck> Failed(this IBlockValidityChecks checks)
        {
            List<BlockValidityCheck>? failed = null;
            foreach (var entry in All)
            {
                if (!entry.Failed(checks)) continue;
                failed ??= new List<BlockValidityCheck>(All.Length);
                failed.Add(entry.Check);
            }
            return (IReadOnlyList<BlockValidityCheck>?)failed ?? Array.Empty<BlockValidityCheck>();
        }

        public static IReadOnlyList<string> FailedIdentifiers(this IBlockValidityChecks checks)
        {
            List<string>? failed = null;
            foreach (var entry in All)
            {
                if (!entry.Failed(checks)) continue;
                failed ??= new List<string>(All.Length);
                failed.Add(entry.Identifier);
            }
            return (IReadOnlyList<string>?)failed ?? Array.Empty<string>();
        }
    }
}
