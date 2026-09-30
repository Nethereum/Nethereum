using Nethereum.Documentation;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Numerics;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM
{
    public partial class TransactionExecutor
    {
        private static readonly EvmUInt256 CallSimulationBalanceCeiling = new EvmUInt256(0x8000000000000000, 0, 0, 0);

#if EVM_SYNC
        [NethereumDocExample(DocSection.EvmSimulator, "simulate-transaction", "Execute one transaction - the EVM_SYNC arm")]
        public TransactionExecutionResult Execute(TransactionExecutionContext ctx)
#else
        [NethereumDocExample(DocSection.EvmSimulator, "simulate-transaction", "Execute one transaction - the async host arm")]
        public async Task<TransactionExecutionResult> ExecuteAsync(TransactionExecutionContext ctx)
#endif
        {
            var result = new TransactionExecutionResult();

            try
            {
                if (ctx.Mode == ExecutionMode.SystemCall)
                    PrepareSystemCall(ctx);
                else
                    ValidateTransaction(ctx, result);

                if (result.IsValidationError)
                    return result;

                #if EVM_SYNC
                SetupState(ctx, result);
#else
                await SetupStateAsync(ctx, result);
#endif
                if (result.IsValidationError)
                    return result;

                #if EVM_SYNC
                ExecuteTransaction(ctx, result);
#else
                await ExecuteTransaction(ctx, result);
#endif

#if EVM_SYNC
                FinalizeTransaction(ctx, result);
#else
                await FinalizeTransactionAsync(ctx, result);
#endif
                CaptureAccountsRemovedFromState(ctx, result);
            }
            catch (TransactionValidationException ex)
            {
                SettleAsAValidationError(result, ex);
            }
            catch (Exception ex) when (BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                SettleAsAFailedTransaction(result, ex);
            }

            return result;
        }

        private static void SettleAsAValidationError(TransactionExecutionResult result, TransactionValidationException ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.ErrorCode = ex.Reason;
            result.IsValidationError = true;
        }

        private static void SettleAsAFailedTransaction(TransactionExecutionResult result, Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

#if EVM_SYNC
        private void SetupState(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task SetupStateAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            Execution.AccessSetWarmUp.WarmOriginPrecompilesAndCoinbase(
                ctx.ExecutionState, _config, ctx.Sender, ctx.Coinbase);

            ctx.SenderAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.Sender);

            #if EVM_SYNC
            EnsureSenderNonceLoaded(ctx);
            EnsureSenderBalanceLoaded(ctx);
            RejectSenderThatIsNotAnEoa(ctx);
#else
            await EnsureSenderNonceLoadedAsync(ctx);
            await EnsureSenderBalanceLoadedAsync(ctx);
            await RejectSenderThatIsNotAnEoaAsync(ctx);
#endif

            var senderBalance = ctx.SenderAccount.Balance.GetTotalBalance();

            var blobBaseFee = BlockBlobBaseFee(ctx);
            ctx.BlobBaseFee = blobBaseFee;
            ctx.BlobGasCost = BlobGasCostOfCarriedBlobs(ctx, blobBaseFee);

            if (ctx.Mode == ExecutionMode.Call && !ctx.EnforceSenderBalance)
            {
                CreditCallSimulationBalance(ctx, senderBalance);
            }
            else if (RejectsForUnaffordableGasAndValue(ctx, result, senderBalance))
            {
                return;
            }

            ctx.SenderNonceBeforeIncrement = (ctx.SenderAccount.Nonce ?? 0UL);

            IncrementSenderNonceOrRejectAtMax(ctx);

            DebitMaxTransactionFeeFromSender(ctx);

            TakePrepPhaseSnapshot(ctx);

            #if EVM_SYNC
            if (!TryApplyTransactionSetupRules(ctx, result))
#else
            if (!await TryApplyTransactionSetupRulesAsync(ctx, result))
#endif
                return;

            TakeDispatchSnapshot(ctx);

            if (ctx.AuthPrepFailed)
                return;

            ProcessAccessList(ctx);

            #if EVM_SYNC
            SetupTargetAccount(ctx);
#else
            await SetupTargetAccountAsync(ctx);
#endif
        }

        /// <summary>
        /// The overlay reads the sender's nonce from the chain only when it holds none, and
        /// independently of the balance load below it. EIP-7928 makes the SET of state reads a
        /// consensus artefact ("Missing or spurious entries invalidate the block"), so the two guards
        /// may not be merged: an overlay carrying a nonce but no initial balance reads only the
        /// balance today, and one helper loading both would read both.
        /// </summary>
#if EVM_SYNC
        private static void EnsureSenderNonceLoaded(TransactionExecutionContext ctx)
#else
        private static async Task EnsureSenderNonceLoadedAsync(TransactionExecutionContext ctx)
#endif
        {
            if (ctx.SenderAccount.Nonce != null)
                return;

#if EVM_SYNC
            ctx.SenderAccount.Nonce = ctx.ExecutionState.StateReader.GetTransactionCount(ctx.Sender);
#else
            ctx.SenderAccount.Nonce = await ctx.ExecutionState.StateReader.GetTransactionCountAsync(ctx.Sender);
#endif
        }

#if EVM_SYNC
        private static void EnsureSenderBalanceLoaded(TransactionExecutionContext ctx)
#else
        private static async Task EnsureSenderBalanceLoadedAsync(TransactionExecutionContext ctx)
#endif
        {
            if (ctx.SenderAccount.Balance.InitialChainBalance != null)
                return;

#if EVM_SYNC
            ctx.SenderAccount.Balance.SetInitialChainBalance(ctx.ExecutionState.StateReader.GetBalance(ctx.Sender));
#else
            ctx.SenderAccount.Balance.SetInitialChainBalance(await ctx.ExecutionState.StateReader.GetBalanceAsync(ctx.Sender));
#endif
        }

        /// <summary>
        /// EIP-3607, "Reject transactions from senders with deployed code": "Any transaction where
        /// <c>tx.sender</c> has a <c>CODEHASH != EMPTYCODEHASH</c> MUST be rejected as invalid".
        /// EIP-7702 supplies the exception: a delegation indicator is not deployed code. The code read
        /// is BAL-recorded and happens only outside call mode, so the guard stays inside this helper
        /// and is never hoisted into a compound condition with the read.
        /// </summary>
#if EVM_SYNC
        private static void RejectSenderThatIsNotAnEoa(TransactionExecutionContext ctx)
#else
        private static async Task RejectSenderThatIsNotAnEoaAsync(TransactionExecutionContext ctx)
#endif
        {
            if (ctx.IsCallMode)
                return;

#if EVM_SYNC
            var senderCode = ctx.ExecutionState.GetCode(ctx.Sender);
#else
            var senderCode = await ctx.ExecutionState.GetCodeAsync(ctx.Sender);
#endif
            if (senderCode != null && senderCode.Length > 0)
            {
                if (!Execution.Eip7702DelegationUtils.IsDelegatedCode(senderCode))
                    throw new TransactionValidationException(TransactionError.SenderNotEOA, "SENDER_NOT_EOA");
            }
        }

        private static void CreditCallSimulationBalance(TransactionExecutionContext ctx, EvmUInt256 senderBalance)
        {
            if (senderBalance < CallSimulationBalanceCeiling)
            {
                ctx.SenderAccount.Balance.CreditExecutionBalance(CallSimulationBalanceCeiling - senderBalance);
            }
        }

        private bool RejectsForUnaffordableGasAndValue(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            EvmUInt256 senderBalance)
        {
            var gasLimitU256 = ctx.GasLimit;
            var gasPriceForCost = ctx.IsEip1559 ? ctx.MaxFeePerGas : ctx.GasPrice;
            var costOverflow = EvmUInt256.BigMul(gasLimitU256, gasPriceForCost, out var gasCost);
            if (!costOverflow.IsZero)
            {
                result.IsValidationError = true;
                result.Error = "Insufficient balance: gas cost overflows 256 bits";
                result.ErrorCode = TransactionError.InsufficientBalance;
                return true;
            }

            var maxBlobReservation = MaxBlobFeeReservation(ctx);
            var maxCost = gasCost + ctx.Value + maxBlobReservation;
            var addOverflow = maxCost < gasCost;
            if (addOverflow || senderBalance < maxCost)
            {
                result.IsValidationError = true;
                result.Error = $"Insufficient balance: {senderBalance} < {maxCost}";
                result.ErrorCode = TransactionError.InsufficientBalance;
                return true;
            }

            return false;
        }

        /// <summary>
        /// EIP-4844, Execution layer validation: "max_total_fee += get_total_blob_gas(tx) *
        /// tx.max_fee_per_blob_gas". The reservation is against the promised maximum, not the block's
        /// BlobBaseFee; the blob fee actually debited by DebitMaxTransactionFeeFromSender still uses
        /// ctx.BlobGasCost = blobGas x BlobBaseFee.
        /// </summary>
        private EvmUInt256 MaxBlobFeeReservation(TransactionExecutionContext ctx)
        {
            if (!ctx.IsType3Transaction || _config.IntrinsicGasRules.Blob == null)
                return EvmUInt256.Zero;

            var blobCount = ctx.BlobVersionedHashes?.Count ?? 0;
            return _config.IntrinsicGasRules.Blob
                .CalculateBlobGasCost(blobCount, ctx.MaxFeePerBlobGas);
        }

        /// <summary>
        /// EIP-2681, "Limit account nonce to 2^64-1": "Consider any transaction invalid, where the
        /// nonce exceeds or equals to <c>2^64-1</c>." The increment runs BEFORE auth-list processing,
        /// which is critical for a self-sponsored transaction where sender = authority.
        /// </summary>
        private static void IncrementSenderNonceOrRejectAtMax(TransactionExecutionContext ctx)
        {
            if (ctx.IsCallMode && !ctx.AdvanceSenderNonce)
                return;

            if (ctx.SenderNonceBeforeIncrement >= ulong.MaxValue)
            {
                if (ctx.SkipNonceMaxCheck)
                {
                    ctx.SenderAccount.Nonce = EvmUInt256.Zero;
                    return;
                }
                throw new TransactionValidationException(TransactionError.NonceIsMax, "NONCE_IS_MAX");
            }

            ctx.SenderAccount.Nonce = ctx.SenderNonceBeforeIncrement + 1;
        }

        private static void DebitMaxTransactionFeeFromSender(TransactionExecutionContext ctx)
        {
            if (ctx.IsCallMode && !ctx.SettleTransactionFees)
                return;

            var gasDeduction = ctx.GasLimit * ctx.EffectiveGasPrice;
            ctx.SenderAccount.Balance.DebitExecutionBalance(gasDeduction);

            if (!ctx.BlobGasCost.IsZero)
                ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.BlobGasCost);
        }

        private static void TakePrepPhaseSnapshot(TransactionExecutionContext ctx)
        {
            ctx.PrepPhaseSnapshotId = ctx.ExecutionState.TakeSnapshot();
        }

#if EVM_SYNC
        private bool TryApplyTransactionSetupRules(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task<bool> TryApplyTransactionSetupRulesAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            if (_config.TransactionSetupRules == null)
                return true;

#if EVM_SYNC
            _config.TransactionSetupRules.ApplyAfterNonceIncrement(ctx, result);
#else
            await _config.TransactionSetupRules.ApplyAfterNonceIncrementAsync(ctx, result);
#endif
            return !result.IsValidationError;
        }

        private static void TakeDispatchSnapshot(TransactionExecutionContext ctx)
        {
            ctx.TransactionSnapshotId = ctx.ExecutionState.TakeSnapshot();
        }

#if EVM_SYNC
        private void SetupTargetAccount(TransactionExecutionContext ctx)
#else
        private async Task SetupTargetAccountAsync(TransactionExecutionContext ctx)
#endif
        {
            ctx.HasCollision = false;

#if EVM_SYNC
            if (ctx.IsContractCreation) SetupCreationTarget(ctx);
            else if (!string.IsNullOrEmpty(ctx.To)) SetupCallTarget(ctx);
#else
            if (ctx.IsContractCreation) await SetupCreationTargetAsync(ctx);
            else if (!string.IsNullOrEmpty(ctx.To)) await SetupCallTargetAsync(ctx);
#endif
        }

#if EVM_SYNC
        private void SetupCreationTarget(TransactionExecutionContext ctx)
#else
        private async Task SetupCreationTargetAsync(TransactionExecutionContext ctx)
#endif
        {
            ctx.ContractAddress = ContractUtils.CalculateContractAddress(ctx.Sender, (long)ctx.SenderNonceBeforeIncrement);
            ctx.ExecutionState.MarkAddressAsWarm(ctx.ContractAddress);
#if EVM_SYNC
            ctx.HasCollision = !AccountDeployability.IsDeployable(ctx.ExecutionState, ctx.ContractAddress);
            if (!ctx.HasCollision) PrepareNewContractAccount(ctx);
#else
            ctx.HasCollision = !await AccountDeployability.IsDeployableAsync(ctx.ExecutionState, ctx.ContractAddress);
            if (!ctx.HasCollision) await PrepareNewContractAccountAsync(ctx);
#endif
            ctx.Code = ctx.HasCollision ? null : ctx.Data;
        }

#if EVM_SYNC
        private void PrepareNewContractAccount(TransactionExecutionContext ctx)
#else
        private async Task PrepareNewContractAccountAsync(TransactionExecutionContext ctx)
#endif
        {
#if EVM_SYNC
            var contractBalance = ctx.ExecutionState.GetTotalBalance(ctx.ContractAddress);
#else
            var contractBalance = await ctx.ExecutionState.GetTotalBalanceAsync(ctx.ContractAddress);
#endif
            ctx.NewAccountStateGasCharged = contractBalance.IsZero;
            ctx.ExecutionState.PrepareNewContractAccount(ctx.ContractAddress, _config.ContractInitialNonce);
        }

#if EVM_SYNC
        private void SetupCallTarget(TransactionExecutionContext ctx)
#else
        private async Task SetupCallTargetAsync(TransactionExecutionContext ctx)
#endif
        {
            ctx.ExecutionState.MarkAddressAsWarm(ctx.To);
#if EVM_SYNC
            ctx.Code = ctx.ExecutionState.GetCode(ctx.To);
            if (_config.TransactionSetupRules != null)
                _config.TransactionSetupRules.ApplyCodeResolution(ctx, null);
#else
            ctx.Code = await ctx.ExecutionState.GetCodeAsync(ctx.To);
            if (_config.TransactionSetupRules != null)
                await _config.TransactionSetupRules.ApplyCodeResolutionAsync(ctx, null);
#endif
        }

#if EVM_SYNC
        private void ExecuteTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecuteTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            if (ctx.CodeResolutionFailed)
            {
                BillTotalForfeitWithoutDispatch(ctx, result);
                return;
            }

            if (ctx.AuthPrepFailed)
            {
                BillTotalForfeitWithoutDispatch(ctx, result);
                return;
            }

            result.GasUsed = ctx.IntrinsicExecutionGas;
            result.Success = !ctx.HasCollision;
            result.GasRefund = 0;

            if (ctx.HasCollision)
            {
                RevertAndBillCollision(ctx, result);
                return;
            }

            if (DispatchesThroughTheInterpreter(ctx))
            {
                #if EVM_SYNC
                ExecuteCode(ctx, result);
#else
                await ExecuteCode(ctx, result);
#endif
            }
            else if (DispatchesToANamedRecipient(ctx))
            {
                #if EVM_SYNC
                ExecutePrecompileOrTransfer(ctx, result);
#else
                await ExecutePrecompileOrTransfer(ctx, result);
#endif
            }
            else if (result.Success && !ctx.Value.IsZero)
            {
                if (TryChargeNewAccountStateGasForEmptyCode(ctx, result))
                    ExecuteSimpleTransfer(ctx, result);
            }
            else if (ctx.IsContractCreation && result.Success)
            {
                if (TryChargeNewAccountStateGasForEmptyCode(ctx, result))
                    _config.ContractCreationMaterialiseRule.Apply(ctx, result);
            }
        }

        private static void BillTotalForfeitWithoutDispatch(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            result.GasUsed = TotalForfeitGasUsed(ctx);
            result.Success = false;
            result.Error = "execution_error";
        }

        /// <summary>
        /// EIP-7610: "If a contract creation is attempted due to a creation transaction, the
        /// <c>CREATE</c> opcode, the <c>CREATE2</c> opcode, or any other reason, and the destination
        /// address already has either a nonzero nonce, a nonzero code length, or non-empty storage,
        /// then the creation MUST throw as if the first byte in the init code were an invalid
        /// opcode." The forfeit rolls back to the DISPATCH snapshot, ctx.TransactionSnapshotId.
        /// </summary>
        private static void RevertAndBillCollision(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            result.GasUsed = TotalForfeitGasUsed(ctx);
            ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
            result.Error = "ADDRESS_COLLISION";
        }

        private static bool DispatchesThroughTheInterpreter(TransactionExecutionContext ctx)
        {
            return ctx.Code != null && ctx.Code.Length > 0;
        }

        private static bool DispatchesToANamedRecipient(TransactionExecutionContext ctx)
        {
            return !ctx.IsContractCreation && !string.IsNullOrEmpty(ctx.To);
        }

        /// <summary>
        /// EIP-8037 (Amsterdam), task #57: apply the NEW_ACCOUNT charge
        /// decided in SetupTargetAccount(Async) for a top-level creation
        /// transaction whose init code is EMPTY. Both callers above
        /// (ExecuteSimpleTransfer's dispatch and
        /// <see cref="Execution.Create.IContractCreationMaterialiseRule"/>'s
        /// dispatch) exist specifically to skip building a <see cref="Program"/>
        /// for a no-op init-code run, so neither ever reaches the charge
        /// ExecuteCode applies at its own NEW_ACCOUNT site (this file, the
        /// "apply the NEW_ACCOUNT charge decided in SetupTargetAccount"
        /// block above) — that site is gated on <c>ctx.Code.Length > 0</c>
        /// one level up in ExecuteTransaction and is structurally
        /// unreachable from here, so this cannot double-charge it.
        /// NEW_ACCOUNT is charged unconditionally at frame-construction,
        /// before any decision about whether init code is empty, so
        /// Nethereum's own code-length-gated shortcut must not silently
        /// drop it either. Mirrors Gas.StateGasMeter.ChargeStateGas's own
        /// two-pool (reservoir-first, spill-into-execution-gas-second)
        /// arithmetic and ExecuteCode's own OutOfGasException handling at
        /// its NEW_ACCOUNT site, without a Program to charge against —
        /// nothing has executed yet on this path, so the full
        /// ctx.ExecutionGasGrant is available to spill into. Returns false
        /// on total forfeit; the caller must not run its dispatch branch.
        /// </summary>
        private bool TryChargeNewAccountStateGasForEmptyCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (!ctx.IsContractCreation || !ctx.NewAccountStateGasCharged || !_config.IntrinsicGasRules.StateGasActive)
                return true;

            return TryChargeStateGasBeforeProgram(ctx, result, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
        }

        /// <summary>
        /// EIP-8037 (Amsterdam), task #59: NEW_ACCOUNT for a value-bearing
        /// top-level CALL that revives a non-alive recipient — charged when
        /// a value transfer to a recipient that is not alive. Keyed on
        /// account aliveness (exists AND non-empty), NOT bare existence
        /// (leaf presence only, a narrower/different predicate).
        /// Applies whether or not the recipient happens to be a precompile
        /// address: charged unconditionally for any non-create top-level
        /// call, with no precompile special-case.
        /// <paramref name="targetIsAlive"/> must be read BEFORE either of
        /// ExecutePrecompileOrTransfer's branches touches the recipient's
        /// balance, mirroring TryValidate/Apply's "read state before you
        /// mutate it" ordering discipline from the AUTH_BASE work.
        /// </summary>
        private bool TryChargeNewAccountStateGasForValueTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result, bool targetIsAlive)
        {
            if (ctx.IsContractCreation || ctx.Value.IsZero || targetIsAlive || !_config.IntrinsicGasRules.StateGasActive)
                return true;

            return TryChargeStateGasBeforeProgram(ctx, result, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
        }

        private bool TryChargeStateGasBeforeProgram(TransactionExecutionContext ctx, TransactionExecutionResult result, long amount)
        {
            if (!ctx.TryChargeStateGas(amount, out var spilledIntoExecution))
            {
                ctx.ExecutionState.RevertToSnapshot(ctx.PrepPhaseSnapshotId);
                result.Success = false;
                result.Error = "execution_error";
                result.GasUsed = TotalForfeitGasUsed(ctx);

                ctx.StateGas = new Gas.StateGasAccount { ReservoirRemaining = ctx.StateGasReservoir };
                ctx.PreDispatchExecutionGasCharged = 0;
                return false;
            }

            result.GasUsed += spilledIntoExecution;
            return true;
        }

        private static void RestoreStateGasToBaseline(
            TransactionExecutionContext ctx,
            Gas.StateGasAccount baseline)
        {
            ctx.StateGas.ReservoirRemaining = baseline.ReservoirRemaining;
            ctx.StateGas.FromReservoir = baseline.FromReservoir;
            ctx.StateGas.SpilledIntoExecution = baseline.SpilledIntoExecution;
        }

#if EVM_SYNC
        private void ExecuteCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecuteCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            var preDispatchStateGasSpilled = ctx.StateGas.SpilledIntoExecution;
            var preDispatchExecutionCharged = ctx.PreDispatchExecutionGasCharged;
            var preDispatchTotal = preDispatchStateGasSpilled + preDispatchExecutionCharged;

            var targetAddress = DispatchTargetAddress(ctx);
            var callContext = BuildTopFrameCallContext(ctx, targetAddress);

            var programContext = BuildTopFrameProgramContext(ctx, callContext);
            PersistTouchesOnRevertWhereCleanupRuns(ctx);

            var program = new Program(ctx.Code, programContext);
            GrantTopFrameStateGasReservoir(ctx, program);

            if (!TryChargeNewAccountStateGasForCreationFrame(ctx, result, program, programContext))
                return;

            DebitTransactionValueFromSender(ctx);
            EmitTransactionTransferLog(ctx, program, targetAddress);

            try
            {
                #if EVM_SYNC
                program = _evmSimulator.ExecuteWithCallStack(program, traceEnabled: ctx.TraceEnabled);
#else
                program = await _evmSimulator.ExecuteWithCallStackAsync(program, traceEnabled: ctx.TraceEnabled);
#endif

                AttachProgramToResult(result, program);
                CaptureTraceWhenEnabled(ctx, result, program);

                result.Success = !program.ProgramResult.IsRevert;

                RestoreTopFrameStateGasOnRevert(result, program);

                SettleDispatchOutcomeOntoResult(ctx, result, program, preDispatchTotal);

                if (result.Success)
                {
                    if (ctx.IsContractCreation)
                    {
                        HandleSuccessfulContractCreation(ctx, result, program);
                    }
                    else
                    {
                        ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
                    }

                    RecordSelfDestructsAndCreatedAccounts(ctx, result, program);
                }
                else
                {
                    RollBackAndBillAnExhaustedGrant(ctx, result, program);
                }

                SettleTransactionStateGas(ctx, program, preDispatchStateGasSpilled);
            }
            catch (Exception)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                throw;
            }
        }

        private static string DispatchTargetAddress(TransactionExecutionContext ctx)
        {
            return ctx.IsContractCreation ? ctx.ContractAddress : ctx.To;
        }

        private static EvmCallContext BuildTopFrameCallContext(TransactionExecutionContext ctx, string targetAddress)
        {
            return new EvmCallContext
            {
                From = ctx.Sender,
                To = targetAddress,
                Data = ctx.IsContractCreation ? new byte[0] : ctx.Data ?? new byte[0],
                Gas = ctx.PreDispatchExecutionGasAvailable,
                Value = ctx.Value,
                Nonce = ctx.Nonce,
                GasPrice = ctx.EffectiveGasPrice,
                ChainId = ctx.ChainId
            };
        }

        private ProgramContext BuildTopFrameProgramContext(TransactionExecutionContext ctx, EvmCallContext callContext)
        {
            var programContext = new ProgramContext(
                callContext,
                ctx.ExecutionState,
                null,
                blockNumber: ctx.BlockNumber,
                timestamp: ctx.Timestamp,
                coinbase: ctx.Coinbase,
                baseFee: ctx.BaseFee,
                codeAddress: ctx.IsContractCreation ? ctx.ContractAddress : null,
                trackAccessList: ctx.TrackAccessList,
                preserveZeroBaseFee: ctx.PreserveZeroBaseFee
            );
            programContext.PrecompileRelocations = ctx.PrecompileRelocations;

            programContext.Difficulty = ctx.Difficulty;
            programContext.GasLimit = ctx.BlockGasLimit;
            programContext.BlobBaseFee = ctx.BlobBaseFee;
            programContext.SlotNumber = ctx.SlotNumber;

            AttachBlobVersionedHashes(ctx, programContext);

            programContext.EnforceSstoreGasStipend = _config.EnforceSstoreGasStipend;
            programContext.BlockHashRule = ctx.BlockHashRuleOverride ?? _config.BlockHashRule;
            // EIP-8037 (Amsterdam): gate every frame's state-gas dimension
            // on the same "are we at Amsterdam+" signal ValidateTransaction
            programContext.StateGasActive = _config.IntrinsicGasRules.StateGasActive;
            programContext.SstoreClearsSchedule = _config.SstoreClearsSchedule;
            programContext.SstoreSetRefund = _config.SstoreSetRefund;
            programContext.SstoreResetRefund = _config.SstoreResetRefund;
            programContext.SstoreRefundRule = _config.SstoreRefundRule;
            programContext.EthTransferLogRule = ctx.EthTransferLogRuleOverride ?? _config.EthTransferLogRule;
            return programContext;
        }

        private static void AttachBlobVersionedHashes(TransactionExecutionContext ctx, ProgramContext programContext)
        {
            if (!ctx.IsType3Transaction || ctx.BlobVersionedHashes == null || ctx.BlobVersionedHashes.Count == 0)
                return;

            var blobHashes = new byte[ctx.BlobVersionedHashes.Count][];
            for (int i = 0; i < ctx.BlobVersionedHashes.Count; i++)
                blobHashes[i] = ctx.BlobVersionedHashes[i].HexToByteArray();
            programContext.BlobHashes = blobHashes;
        }

        private void PersistTouchesOnRevertWhereCleanupRuns(TransactionExecutionContext ctx)
        {
            ctx.ExecutionState.TouchPersistsOnRevert = _config.CleanEmptyAccounts;
        }

        private static void GrantTopFrameStateGasReservoir(TransactionExecutionContext ctx, Program program)
        {
            program.StateGasLeft = ctx.StateGas.ReservoirRemaining;
            program.StateGasBaseline = ctx.StateGas.ReservoirRemaining;
        }

        private static void DebitTransactionValueFromSender(TransactionExecutionContext ctx)
        {
            if (ctx.Value.IsZero)
                return;

            ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);
        }

        private void EmitTransactionTransferLog(TransactionExecutionContext ctx, Program program, string targetAddress)
        {
            var transferRule = ctx.EthTransferLogRuleOverride ?? _config.EthTransferLogRule;
            transferRule.Emit(program.ProgramResult.Logs, ctx.Sender, targetAddress, ctx.Value);
        }

        private static void AttachProgramToResult(TransactionExecutionResult result, Program program)
        {
            result.Program = program;
        }

        private static void CaptureTraceWhenEnabled(TransactionExecutionContext ctx, TransactionExecutionResult result, Program program)
        {
            if (!ctx.TraceEnabled)
                return;

            result.Traces = program.Trace;
        }

        private static void RestoreTopFrameStateGasOnRevert(TransactionExecutionResult result, Program program)
        {
            if (result.Success)
                return;

            Gas.StateGasMeter.RestoreStateGas(program);
        }

        private static void SettleDispatchOutcomeOntoResult(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program,
            long preDispatchTotal)
        {
            result.GasUsed = ctx.IntrinsicExecutionGas + program.TotalGasUsed + preDispatchTotal;
            result.ReturnData = program.ProgramResult.Result;

            if (result.Success)
            {
                result.GasRefund = program.RefundCounter;
            }

#if EVM_SYNC
            if (program.ProgramResult.IsRevert)
            {
                if (program.HasExecutionError)
                    result.Error = "execution_error";
                else
                    result.Error = "revert";
            }
#else
            result.RevertReason = program.ProgramResult.GetRevertMessage();
#endif

            result.ProgramResult = program.ProgramResult;
            if (result.Success)
                result.Logs = program.ProgramResult.Logs;
            result.InnerCalls = program.ProgramResult.InnerCalls;
            result.InnerContractCodeCalls = program.ProgramResult.InnerContractCodeCalls;
        }

        private static void RecordSelfDestructsAndCreatedAccounts(TransactionExecutionContext ctx, TransactionExecutionResult result, Program program)
        {
            if (!result.Success)
                return;

            MarkSelfDestructedContractsAsRemoved(ctx, program);
            ClearSelfDestructedContractsPreservingBalance(ctx, program);

            result.CreatedAccounts = new List<string>(program.ProgramResult.CreatedContractAccounts);
        }

        private static void RollBackAndBillAnExhaustedGrant(TransactionExecutionContext ctx, TransactionExecutionResult result, Program program)
        {
            ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
            if (program.GasRemaining == 0)
                result.GasUsed = TotalForfeitGasUsed(ctx);
        }

        private static void SettleTransactionStateGas(TransactionExecutionContext ctx, Program program, long preDispatchStateGasSpilled)
        {
            ctx.StateGas = new Gas.StateGasAccount
            {
                ReservoirRemaining = program.StateGasLeft,
                FromReservoir = ctx.StateGasReservoir - program.StateGasLeft,
                SpilledIntoExecution = program.StateGasSpilled + preDispatchStateGasSpilled
            };
        }

        private static bool TryChargeNewAccountStateGasForCreationFrame(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program,
            ProgramContext programContext)
        {
            if (!ctx.IsContractCreation || !ctx.NewAccountStateGasCharged || !programContext.StateGasActive)
                return true;

#if EVM_SYNC
            Gas.StateGasMeter.ChargeStateGas(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            if (program.HasExecutionError)
            {
                ForfeitTransactionBeforeDispatch(ctx, result);
                return false;
            }
#else
            try
            {
                Gas.StateGasMeter.ChargeStateGas(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            }
            catch (Nethereum.EVM.Exceptions.OutOfGasException)
            {
                ForfeitTransactionBeforeDispatch(ctx, result);
                return false;
            }
#endif
            return true;
        }

        private static void ForfeitTransactionBeforeDispatch(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
            result.Success = false;
            result.Error = "execution_error";
            result.GasUsed = TotalForfeitGasUsed(ctx);
        }

#if EVM_SYNC
        private void ExecutePrecompileOrTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecutePrecompileOrTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            var registry = _config.Precompiles;
            int precompileAddr;
            var isPrecompile = Execution.Precompiles.PrecompileRelocationResolver.TryResolve(
                ctx.PrecompileRelocations,
                registry,
                Execution.Precompiles.PrecompileRelocationResolver.Normalize(ctx.To),
                out precompileAddr);

#if EVM_SYNC
            var targetIsAlive = ctx.ExecutionState.AccountExists(ctx.To);
#else
            var targetIsAlive = await ctx.ExecutionState.AccountExistsAsync(ctx.To);
#endif
            var stateGasBeforeValueTransferCharge = CaptureStateGasBaseline(ctx);

            if (!TryChargeNewAccountStateGasForValueTransfer(ctx, result, targetIsAlive))
                return;

            if (isPrecompile)
            {
                DispatchTopLevelPrecompile(ctx, result, registry, precompileAddr, stateGasBeforeValueTransferCharge);
            }
            else
            {
                SettleTopLevelValueTransfer(ctx, result);
            }
        }

        private static bool TryResolveWiredPrecompile(Execution.Precompiles.PrecompileRegistry registry, string to, out int precompileAddr)
        {
            precompileAddr = -1;
            return registry != null
                && TryParsePrecompileAddress(to, out precompileAddr)
                && registry.CanHandle(precompileAddr);
        }

        private static Gas.StateGasAccount CaptureStateGasBaseline(TransactionExecutionContext ctx)
        {
            return new Gas.StateGasAccount
            {
                ReservoirRemaining = ctx.StateGas.ReservoirRemaining,
                FromReservoir = ctx.StateGas.FromReservoir,
                SpilledIntoExecution = ctx.StateGas.SpilledIntoExecution
            };
        }

        private static void FailTopLevelPrecompileDispatch(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Gas.StateGasAccount stateGasBaseline,
            string error)
        {
            RestoreStateGasToBaseline(ctx, stateGasBaseline);
            result.GasUsed = TotalForfeitGasUsed(ctx);
            result.Success = false;
            result.Error = error;
            ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
        }

        private void DispatchTopLevelPrecompile(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Execution.Precompiles.PrecompileRegistry registry,
            int precompileAddr,
            Gas.StateGasAccount stateGasBaseline)
        {
            var precompileInput = ctx.Data ?? new byte[0];
            var precompileGasCost = registry.GetGasCost(precompileAddr, precompileInput);
            var availableGas = ctx.PreDispatchExecutionGasAvailable;

            if (precompileGasCost > availableGas)
            {
                FailTopLevelPrecompileDispatch(ctx, result, stateGasBaseline, "PRECOMPILE_OUT_OF_GAS");
                return;
            }

            result.GasUsed = ctx.IntrinsicExecutionGas + precompileGasCost
                + ctx.PreDispatchExecutionGasConsumed;
            try
            {
                result.ReturnData = registry.Execute(precompileAddr, precompileInput);
                result.Success = true;

                if (!ctx.Value.IsZero)
                {
                    ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);
                    var receiverAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.To);
                    receiverAccount.Balance.CreditExecutionBalance(ctx.Value);
                }

                ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);

                var transferRule = ctx.EthTransferLogRuleOverride ?? _config.EthTransferLogRule;
                transferRule.Emit(result.Logs, ctx.Sender, ctx.To, ctx.Value);
            }
            catch (Exception ex) when (!BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                FailTopLevelPrecompileDispatch(ctx, result, stateGasBaseline, "PRECOMPILE_FAILED");
            }
        }

        private void SettleTopLevelValueTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            result.GasUsed = ctx.IntrinsicExecutionGas + ctx.PreDispatchExecutionGasConsumed;

            if (!ctx.Value.IsZero)
            {
                ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);
            }
            var receiverAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.To);
            receiverAccount.Balance.CreditExecutionBalance(ctx.Value);
            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);

            var transferRule = ctx.EthTransferLogRuleOverride ?? _config.EthTransferLogRule;
            transferRule.Emit(result.Logs, ctx.Sender, ctx.To, ctx.Value);
        }

        private static bool TryParsePrecompileAddress(string checksumAddress, out int addressInt)
        {
            addressInt = -1;
            if (string.IsNullOrEmpty(checksumAddress)) return false;
            var compact = checksumAddress.ToHexCompact();
            return int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out addressInt);
        }

    }
}
