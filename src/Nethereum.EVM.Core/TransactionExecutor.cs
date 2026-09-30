using System;
using System.Collections.Generic;
#if !EVM_SYNC
using System.Numerics;
using System.Threading.Tasks;
#endif
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif
#if !EVM_SYNC
using Nethereum.Signer;
#endif
using Nethereum.Util;

namespace Nethereum.EVM
{
    public partial class TransactionExecutor
    {
        private readonly EVMSimulator _evmSimulator;
        public EVMSimulator GetSimulator() => _evmSimulator;
        private readonly HardforkConfig _config;

        private static long TotalForfeitGasUsed(TransactionExecutionContext ctx) => ctx.TotalForfeitGasUsed();

        public TransactionExecutor(HardforkConfig config, EVMSimulator evmSimulator = null)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
            _evmSimulator = evmSimulator ?? new EVMSimulator(_config);
        }


        private void ValidateTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.BlockGasCapacity.CheckGasAllowanceFits(ctx, _config);

            ResolveEffectiveGasPrice(ctx);
            RejectOversizedInitcode(ctx);

            var isSelfTransfer = !ctx.IsContractCreation && ctx.Sender.IsTheSameAddress(ctx.To);
            var hasValue = !ctx.Value.IsZero;

            ctx.IntrinsicExecutionGas = _config.IntrinsicGasRules.CalculateIntrinsicGas(ctx.Data, ctx.IsContractCreation, ctx.AccessList, isSelfTransfer, hasValue);

            RejectUnderpricedBlobFee(ctx);

            _config.TransactionValidationRules?.Validate(ctx, _config);

            AllocateEvmGas(ctx);
            OpenStateGasAccount(ctx);

            ComputeCalldataFloor(ctx, isSelfTransfer, hasValue);
            if (RejectsForFloorAboveExecutionGasCap(ctx, result))
                return;
            if (RejectsForGasBelowMinimum(ctx, result))
                return;

            ctx.BlockGasCapacity.CheckBlobsFit(ctx, _config);
        }

        /// <summary>
        /// The entry an <see cref="ExecutionMode.SystemCall"/> takes in place
        /// of <see cref="ValidateTransaction"/>. A system call is issued by the
        /// protocol rather than submitted by anyone: there is no fee to price,
        /// no intrinsic charge to deduct, no declared gas limit to test and no
        /// calldata floor to meet, so the fork's budget is installed here and
        /// nothing later overwrites it.
        ///
        /// <para>EIP-8037: "System calls remain not subject to the [EIP-7825]
        /// <c>TX_MAX_GAS_LIMIT</c> cap, do not count against the block gas
        /// limit".</para>
        ///
        /// <para>Checked against EELS <c>amsterdam/fork.py:756-778</c>, which
        /// gives the system call its own <c>TransactionEnvironment</c> with
        /// <c>gas_limit</c>, <c>execution_gas_grant</c> and
        /// <c>state_gas_reservoir</c> stated outright and
        /// <c>calldata_floor=Uint(0)</c>, rather than deriving any of them from
        /// a transaction.</para>
        /// </summary>
        private void PrepareSystemCall(TransactionExecutionContext ctx)
        {
            var budget = _config.SystemCallGas;
            ctx.EffectiveGasPrice = EvmUInt256.Zero;
            ctx.IntrinsicExecutionGas = 0;
            ctx.GasLimit = new EvmUInt256(budget.ExecutionGas);
            ctx.ExecutionGasGrant = budget.ExecutionGas;
            ctx.StateGasReservoir = budget.StateGasReservoir;
            OpenStateGasAccount(ctx);
        }

        private static void ResolveEffectiveGasPrice(TransactionExecutionContext ctx)
        {
            if (ctx.IsCallMode && !ctx.SettleTransactionFees)
            {
                ctx.EffectiveGasPrice = EvmUInt256.Zero;
                return;
            }

            if (ctx.IsEip1559)
            {
                if (ctx.MaxFeePerGas < ctx.BaseFee)
                {
                    if (!ctx.AllowFeeCapBelowBaseFee)
                        throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerGas, "INSUFFICIENT_MAX_FEE_PER_GAS");

                    ctx.EffectiveGasPrice = ctx.MaxFeePerGas;
                    return;
                }

                if (ctx.MaxPriorityFeePerGas > ctx.MaxFeePerGas)
                    throw new TransactionValidationException(TransactionError.PriorityGreaterThanMaxFee, "PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS");

                var diff = ctx.MaxFeePerGas - ctx.BaseFee;
                var priorityFee = ctx.MaxPriorityFeePerGas < diff ? ctx.MaxPriorityFeePerGas : diff;
                ctx.EffectiveGasPrice = ctx.BaseFee + priorityFee;
                return;
            }

            ctx.EffectiveGasPrice = ctx.GasPrice;

            if (!ctx.BaseFee.IsZero && ctx.GasPrice < ctx.BaseFee && !ctx.AllowFeeCapBelowBaseFee)
                throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerGas, "INSUFFICIENT_MAX_FEE_PER_GAS");
        }

        private void RejectOversizedInitcode(TransactionExecutionContext ctx)
        {
            if (_config.MaxInitcodeSize > 0 && ctx.IsContractCreation && ctx.Data != null && ctx.Data.Length > _config.MaxInitcodeSize)
                throw new TransactionValidationException(TransactionError.InitcodeSizeExceeded, "INITCODE_SIZE_EXCEEDED");
        }

        private void RejectUnderpricedBlobFee(TransactionExecutionContext ctx)
        {
            if (!ctx.IsType3Transaction || _config.IntrinsicGasRules.Blob == null)
                return;

            if (ctx.MaxFeePerBlobGas < BlockBlobBaseFee(ctx))
                throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerBlobGas, "INSUFFICIENT_MAX_FEE_PER_BLOB_GAS");
        }

        /// <summary>
        /// EIP-7516: "<c>BLOBBASEFEE</c> returns the result of the
        /// <c>get_blob_gasprice(header) -&gt; int</c> function as defined in EIP-4844
        /// §Gas accounting" — the block's excess blob gas is its only input, so no
        /// property of the transaction takes part.
        ///
        /// <para>Zero before Cancun, the fork that first installs an
        /// <see cref="Gas.Intrinsic.IBlobGasRule"/>: there is no blob gasprice to
        /// report, no transaction may carry a blob, and <c>BLOBBASEFEE</c> is not
        /// registered on the opcode table (<c>ContextExecutor.hasBlobOps</c>).</para>
        /// </summary>
        private EvmUInt256 BlockBlobBaseFee(TransactionExecutionContext ctx)
        {
            var blobRule = _config.IntrinsicGasRules.Blob;
            return blobRule == null
                ? EvmUInt256.Zero
                : blobRule.CalculateBlobBaseFee(ctx.ExcessBlobGas);
        }

        private EvmUInt256 BlobGasCostOfCarriedBlobs(TransactionExecutionContext ctx, EvmUInt256 blobBaseFee)
        {
            var blobRule = _config.IntrinsicGasRules.Blob;
            if (blobRule == null || !ctx.IsType3Transaction)
                return EvmUInt256.Zero;

            return blobRule.CalculateBlobGasCost(ctx.BlobVersionedHashes?.Count ?? 0, blobBaseFee);
        }

        /// <summary>
        /// EIP-8037 (AMS-8037-02/03): splits the post-intrinsic EVM gas
        /// between the execution grant and the state-gas reservoir.
        ///
        /// <para>Gated on <c>IntrinsicGasRules.StateGasActive</c> (true only
        /// from Amsterdam's EIP-2780 decomposition — the same signal
        /// <c>FinalizeTransaction</c> uses to detect "are we at Amsterdam+")
        /// so pre-Amsterdam forks keep the exact pre-existing formula:
        /// <c>ExecutionGasGrant = GasLimit - Intrinsic</c>, no reservoir.</para>
        /// </summary>
        private void AllocateEvmGas(TransactionExecutionContext ctx)
        {
            var evmGas = ctx.GasLimit.ToLongSafe() - ctx.IntrinsicExecutionGas;
            if (_config.IntrinsicGasRules.StateGasActive)
            {
                var executionBudget = GasConstants.EIP8037_TX_MAX_GAS_LIMIT - ctx.IntrinsicExecutionGas;
                ctx.ExecutionGasGrant = Math.Min(executionBudget, evmGas);
                ctx.StateGasReservoir = evmGas - ctx.ExecutionGasGrant;
            }
            else
            {
                ctx.ExecutionGasGrant = evmGas;
                ctx.StateGasReservoir = 0;
            }
        }

        private static void OpenStateGasAccount(TransactionExecutionContext ctx)
        {
            ctx.StateGas = new Gas.StateGasAccount { ReservoirRemaining = ctx.StateGasReservoir };
        }

        /// <summary>
        /// EIP-7623 calldata floor. The rule being non-null is the "floor
        /// active at this fork" signal — no EIP flag consulted; it returns 0
        /// pre-Prague. ctx.AccessList is the SAME access list
        /// CalculateIntrinsicGas was given (EIP-7981, Amsterdam+), and
        /// ctx.IsContractCreation must be passed through unchanged: a no-op for
        /// the pre-Amsterdam floor shape (task #56 — see
        /// CalculateFloorGasLimit's own doc comment) and load-bearing above it,
        /// where settlement recomputes the floor and must arrive at this same
        /// value.
        /// </summary>
        private void ComputeCalldataFloor(TransactionExecutionContext ctx, bool isSelfTransfer, bool hasValue)
        {
            ctx.MinGasRequired = ctx.IntrinsicExecutionGas;
            ctx.FloorGas = _config.IntrinsicGasRules.CalculateFloorGasLimit(ctx.Data, ctx.IsContractCreation, isSelfTransfer, hasValue, ctx.AccessList);
            if (ctx.FloorGas > ctx.MinGasRequired)
                ctx.MinGasRequired = ctx.FloorGas;
        }

        private bool RejectsForFloorAboveExecutionGasCap(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.IsCallMode || !_config.IntrinsicGasRules.StateGasActive || ctx.FloorGas <= GasConstants.EIP8037_TX_MAX_GAS_LIMIT)
                return false;

            result.IsValidationError = true;
            result.Error = "INTRINSIC_GAS_TOO_LOW";
            result.ErrorCode = TransactionError.IntrinsicGasTooLow;
            return true;
        }

        private static bool RejectsForGasBelowMinimum(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.GasLimit >= ctx.MinGasRequired)
                return false;

            result.IsValidationError = true;
            result.Error = $"Intrinsic gas too low: {ctx.GasLimit} < {ctx.MinGasRequired}";
            result.ErrorCode = TransactionError.IntrinsicGasTooLow;
            return true;
        }

        private void ProcessAccessList(TransactionExecutionContext ctx)
        {
            if (ctx.AccessList == null)
                return;

            foreach (var entry in ctx.AccessList)
            {
                ctx.ExecutionState.MarkAddressAsWarm(entry.Address);
                var warmAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(entry.Address);
                if (entry.StorageKeys != null)
                {
                    foreach (var storageKey in entry.StorageKeys)
                    {
                        var slot = EvmUInt256.FromBigEndian(storageKey.HexToByteArray());
                        warmAccount.MarkStorageKeyAsWarm(slot);
                    }
                }
            }
        }

        private void RevertContractCreationAfterInit(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program,
            string error)
        {
            Gas.StateGasMeter.RestoreStateGas(program);
            result.Success = false;
            ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
            result.GasUsed = TotalForfeitGasUsed(ctx);
            result.Error = error;
            result.Logs.Clear();
        }

        private void HandleSuccessfulContractCreation(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program)
        {
            var deployedCode = program.ProgramResult.Result ?? new byte[0];

            if (_config.RejectEfPrefix && deployedCode.Length > 0 && deployedCode[0] == 0xEF)
            {
                RevertContractCreationAfterInit(ctx, result, program, "INVALID_EF_PREFIX");
                return;
            }

            if (_config.MaxCodeSize > 0 && deployedCode.Length > _config.MaxCodeSize)
            {
                RevertContractCreationAfterInit(ctx, result, program, "MAX_CODE_SIZE_EXCEEDED");
                return;
            }

            var availableExecutionGas = _config.IntrinsicGasRules.StateGasActive
                ? ctx.ExecutionGasGrant - (result.GasUsed - ctx.IntrinsicExecutionGas)
                : ctx.GasLimit.ToLongSafe() - result.GasUsed;
            var depositResult = Execution.Create.CodeDepositCharger.Charge(program, deployedCode, _config, availableExecutionGas);
            if (depositResult.Failed)
            {
                RevertContractCreationAfterInit(ctx, result, program, "CODE_DEPOSIT_OUT_OF_GAS");
                return;
            }
            deployedCode = depositResult.FinalCode;
            var codeDepositGas = depositResult.FinalCodeDepositCost;

            result.GasUsed += codeDepositGas;
            var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
            newContractAccount.Code = deployedCode;
            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            result.ContractAddress = ctx.ContractAddress;
        }

        private static void MarkSelfDestructedContractsAsRemoved(TransactionExecutionContext ctx, Program program)
        {
            foreach (var deletedAddr in program.ProgramResult.DeletedContractAccounts)
            {
                ctx.ExecutionState.DeleteAccount(deletedAddr);
            }
        }

        private static void ClearSelfDestructedContractsPreservingBalance(TransactionExecutionContext ctx, Program program)
        {
            foreach (var clearedAddr in program.ProgramResult.ClearedContractAccounts)
            {
                ctx.ExecutionState.ClearAccountPreservingBalance(clearedAddr);
            }
        }

        private static void CaptureAccountsRemovedFromState(
            TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            var removed = new List<string>();
            foreach (var pair in ctx.ExecutionState.AccountsState)
            {
                if (pair.Value.IsRemoved) removed.Add(pair.Key.ToHexLower());
            }
            result.DeletedAccounts = removed;
        }

        private void ExecuteSimpleTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);

            if (ctx.IsContractCreation)
            {
                var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
                newContractAccount.Balance.CreditExecutionBalance(ctx.Value);

                var transferRule = ctx.EthTransferLogRuleOverride ?? _config.EthTransferLogRule;
                transferRule.Emit(result.Logs, ctx.Sender, ctx.ContractAddress, ctx.Value);
            }

            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
        }

#if EVM_SYNC
        private void FinalizeTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task FinalizeTransactionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            CapGasUsedAtGasLimit(ctx, result);
            var preRefundTotal = PreRefundGasTotal(ctx, result);
            ApplyGasRefunds(ctx, result, preRefundTotal);
            var floorDataGas = RaiseGasUsedToCalldataFloor(ctx, result);
            RecordSettledGas(ctx, result, preRefundTotal, floorDataGas);

            if (ctx.IsCallMode && !ctx.SettleTransactionFees)
                return;

            RefundUnspentGasToSender(ctx, result);
#if EVM_SYNC
            PayTheCoinbase(ctx, result);
#else
            await PayTheCoinbaseAsync(ctx, result);
#endif
            _config.TouchedEmptyCleanupRule.Apply(ctx.ExecutionState);
        }

        private static void CapGasUsedAtGasLimit(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (result.GasUsed > ctx.GasLimit)
                result.GasUsed = ctx.GasLimit.ToLongSafe();
        }

        private static long PreRefundGasTotal(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            return result.GasUsed + ctx.StateGas.FromReservoir;
        }

        private void ApplyGasRefunds(TransactionExecutionContext ctx, TransactionExecutionResult result, long preRefundTotal)
        {
            var totalRefundCounter = ctx.AuthRefund;
            if (result.Success)
                totalRefundCounter += result.GasRefund;

            var maxRefund = preRefundTotal / _config.RefundQuotient;
            var appliedRefund = Math.Min(totalRefundCounter, maxRefund);
            result.GasUsed = preRefundTotal - appliedRefund;
            result.GasRefund = appliedRefund;
        }

        private long RaiseGasUsedToCalldataFloor(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            var isSelfTransferAtFinalisation = !ctx.IsContractCreation && ctx.Sender.IsTheSameAddress(ctx.To);
            var hasValueAtFinalisation = !ctx.Value.IsZero;
            var floorIsContractCreation = _config.IntrinsicGasRules.StateGasActive && ctx.IsContractCreation;
            var floorDataGas = _config.IntrinsicGasRules.CalculateFloorGasLimit(ctx.Data, floorIsContractCreation,
                isSelfTransfer: isSelfTransferAtFinalisation, hasValue: hasValueAtFinalisation, accessList: ctx.AccessList);

            if (floorDataGas > 0 && result.GasUsed < floorDataGas)
            {
                result.GasUsed = floorDataGas;
                CapGasUsedAtGasLimit(ctx, result);
            }

            return floorDataGas;
        }

        private static void RecordSettledGas(
            TransactionExecutionContext ctx, TransactionExecutionResult result, long preRefundTotal, long floorDataGas)
        {
            result.EffectiveGasUsed = result.GasUsed;

            var stateGasUsedForBlock = Math.Max(0L, ctx.StateGasUsed);
            result.StateGasUsed = stateGasUsedForBlock;
            result.ExecutionGasUsed = Math.Max(preRefundTotal - stateGasUsedForBlock, floorDataGas);
            result.IntrinsicGasUsed = ctx.IntrinsicExecutionGas;
        }

        private static void RefundUnspentGasToSender(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            var gasRefundAmount = (ctx.GasLimit - new EvmUInt256(result.GasUsed)) * ctx.EffectiveGasPrice;
            ctx.SenderAccount.Balance.CreditExecutionBalance(gasRefundAmount);
        }

#if EVM_SYNC
        private void PayTheCoinbase(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task PayTheCoinbaseAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            var perGasReward = _config.BaseFeeApplies
                ? (ctx.EffectiveGasPrice > ctx.BaseFee ? ctx.EffectiveGasPrice - ctx.BaseFee : EvmUInt256.Zero)
                : ctx.EffectiveGasPrice;
            var minerReward = new EvmUInt256(result.GasUsed) * perGasReward;

#if EVM_SYNC
            var coinbaseAccount = ctx.ExecutionState.LoadBalanceNonceAndCodeFromStorage(ctx.Coinbase);
#else
            var coinbaseAccount = await ctx.ExecutionState.LoadBalanceNonceAndCodeFromStorageAsync(ctx.Coinbase);
#endif
            if (!minerReward.IsZero)
                coinbaseAccount.Balance.CreditExecutionBalance(minerReward);
            else
                coinbaseAccount.IsTouched = true;
        }
    }

    public class TransactionValidationException : Exception
    {
        public TransactionValidationException(TransactionError reason, string message) : base(message)
        {
            Reason = reason;
        }

        public TransactionError Reason { get; }
    }
}
