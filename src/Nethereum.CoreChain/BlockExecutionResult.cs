using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public sealed class BlockExecutionResult : IBlockValidityChecks
    {
        public required HardforkName Fork { get; init; }

        public required byte[]? PreStateRoot { get; init; }

        public required byte[]? PostStateRoot { get; init; }

        public required IReadOnlyList<TransactionExecutionResult> Receipts { get; init; }

        public required IReadOnlyList<Log> Logs { get; init; }

        public byte[]? BlockBloom { get; init; }

        public byte[]? ComputedRequestsHash { get; init; }

        public IReadOnlyList<byte[]>? ExecutionRequests { get; init; }

        public bool RequestsHashMismatch { get; init; }

        public byte[]? WitnessBytes { get; init; }

        public BlockExecutionStats? Stats { get; init; }

        public BigInteger MinerRewardCredited { get; init; }

        public int WithdrawalsCredited { get; init; }

        public bool StateRootMismatch { get; init; }

        public System.Numerics.BigInteger GasUsed { get; init; }

        public bool ReceiptsRootMismatch { get; init; }

        public bool LogsBloomMismatch { get; init; }

        public bool GasUsedMismatch { get; init; }

        public bool GasCapacityExceeded { get; init; }

        public bool BlobGasCapacityExceeded { get; init; }

        public bool BlobGasUsedMismatch { get; init; }

        public bool ExcessBlobGasMismatch { get; init; }

        public bool BlobFieldFormatMismatch { get; init; }

        public bool BaseFeeMismatch { get; init; }

        public bool BlockAccessListHashMismatch { get; init; }

        public bool BlockAccessListGasLimitExceeded { get; init; }

        public bool BlockAccessListMalformed { get; init; }

        public bool ContainsInvalidTransaction { get; init; }

        public bool GasLimitBoundViolated { get; init; }

        public bool WithdrawalsRootMismatch { get; init; }

        public bool BlockRlpSizeExceeded { get; init; }

        public bool UnknownParent { get; init; }

        public bool BlockNumberMismatch { get; init; }

        public bool TimestampNotAfterParent { get; init; }

        public bool FutureBlock { get; init; }

        public bool ExtraDataTooLong { get; init; }

        public bool DaoExtraDataMismatch { get; init; }

        public bool GasLimitTooBig { get; init; }

        public bool DifficultyMismatch { get; init; }

        public bool NonceInvalid { get; init; }

        public bool UnclesHashMismatch { get; init; }

        public bool UnclesInvalid { get; init; }

        public bool TransactionsRootMismatch { get; init; }

        public bool WithdrawalsRootPresenceMismatch { get; init; }

        public bool ParentBeaconRootPresenceMismatch { get; init; }

        public bool BlockAccessListHashPresenceMismatch { get; init; }

        public bool SlotNumberPresenceMismatch { get; init; }

        public bool ParentNotHead { get; init; }

        public List<AccountChanges>? BlockAccessList { get; init; }

        public long BlockExecutionGasUsed { get; init; }

        public long BlockStateGasUsed { get; init; }

        public bool ExecutionValidityMismatch => this.AnyFailed();

        public IReadOnlyList<BlockValidityCheck> FailedValidityChecks => this.Failed();

        public IReadOnlyList<string> FailedChecks => this.FailedIdentifiers();

        public TransactionError InvalidTransactionReason
        {
            get
            {
                var index = InvalidTransactionIndex;
                return index < 0 ? TransactionError.None : Receipts[index].ErrorCode;
            }
        }

        public int InvalidTransactionIndex
        {
            get
            {
                for (var i = 0; i < Receipts.Count; i++)
                    if (Receipts[i].ErrorCode != TransactionError.None) return i;
                return -1;
            }
        }

        public Exception? Exception { get; init; }

        public int FailedTransactionIndex { get; init; } = -1;

        public string? ErrorMessage { get; init; }
    }
}
