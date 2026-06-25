using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
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
#if EVM_SYNC
        public TransactionExecutionResult Execute(TransactionExecutionContext ctx)
#else
        public async Task<TransactionExecutionResult> ExecuteAsync(TransactionExecutionContext ctx)
#endif
        {
            var result = new TransactionExecutionResult();

            try
            {
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

                FinalizeTransaction(ctx, result);
            }
            catch (TransactionValidationException ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.IsValidationError = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

#if EVM_SYNC
        private void SetupState(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task SetupStateAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            ctx.ExecutionState.MarkAddressAsWarm(ctx.Sender);
            ctx.ExecutionState.MarkPrecompilesAsWarm(_config.Precompiles);

            // EIP-3651 (Shanghai+): coinbase pre-warmed. Pre-Shanghai forks
            // (Berlin/London/Paris) leave the coinbase COLD — first access
            // costs 2600 (cold) not 100 (warm).
            if (_config.WarmCoinbase && !string.IsNullOrEmpty(ctx.Coinbase))
                ctx.ExecutionState.MarkAddressAsWarm(ctx.Coinbase);

            ctx.SenderAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.Sender);

            if (ctx.SenderAccount.Nonce == null)
            {
                #if EVM_SYNC
                ctx.SenderAccount.Nonce = ctx.ExecutionState.StateReader.GetTransactionCount(ctx.Sender);
#else
                ctx.SenderAccount.Nonce = await ctx.ExecutionState.StateReader.GetTransactionCountAsync(ctx.Sender);
#endif
            }

            if (ctx.SenderAccount.Balance.InitialChainBalance == null)
            {
                #if EVM_SYNC
                ctx.SenderAccount.Balance.SetInitialChainBalance(ctx.ExecutionState.StateReader.GetBalance(ctx.Sender));
#else
                ctx.SenderAccount.Balance.SetInitialChainBalance(await ctx.ExecutionState.StateReader.GetBalanceAsync(ctx.Sender));
#endif
            }

            if (!ctx.IsCallMode)
            {
                #if EVM_SYNC
                var senderCode = ctx.ExecutionState.GetCode(ctx.Sender);
#else
                var senderCode = await ctx.ExecutionState.GetCodeAsync(ctx.Sender);
#endif
                if (senderCode != null && senderCode.Length > 0)
                {
                    if (!Execution.Eip7702DelegationUtils.IsDelegatedCode(senderCode))
                        throw new TransactionValidationException("SENDER_NOT_EOA");
                }
            }

            var senderBalance = ctx.SenderAccount.Balance.GetTotalBalance();

            ctx.BlobGasCost = 0;
            ctx.BlobBaseFee = EvmUInt256.One;
            var blobRule = _config.IntrinsicGasRules.Blob;
            if (ctx.IsType3Transaction && blobRule != null)
            {
                ctx.BlobBaseFee = blobRule.CalculateBlobBaseFee(ctx.ExcessBlobGas);
                var blobCount = ctx.BlobVersionedHashes?.Count ?? 0;
                ctx.BlobGasCost = blobRule.CalculateBlobGasCost(blobCount, ctx.BlobBaseFee);
            }

            if (ctx.Mode == ExecutionMode.Call)
            {
                var halfMax = new EvmUInt256(0x8000000000000000, 0, 0, 0);
                if (senderBalance < halfMax)
                {
                    ctx.SenderAccount.Balance.CreditExecutionBalance(halfMax - senderBalance);
                }
            }
            else
            {
                var gasLimitU256 = ctx.GasLimit;
                var gasPriceForCost = ctx.IsEip1559 ? ctx.MaxFeePerGas : ctx.GasPrice;
                var costOverflow = EvmUInt256.BigMul(gasLimitU256, gasPriceForCost, out var gasCost);
                if (!costOverflow.IsZero)
                {
                    result.IsValidationError = true;
                    result.Error = "Insufficient balance: gas cost overflows 256 bits";
                    return;
                }

                // EIP-4844 sender balance reservation uses MaxFeePerBlobGas
                // (the user's promised maximum), NOT the block's actual
                // BlobBaseFee. The actual blob fee paid in DebitExecutionBalance
                // below still uses BlobGasCost = blobGas × BlobBaseFee — only
                // the reservation check here must be against the max promise
                // so a tx whose actual blob fee fits but max-promise doesn't
                // is correctly rejected. Caught by EEST
                // cancun/eip4844_blobs/test_blob_txs.py::test_insufficient_balance_blob_tx
                // (60 cases × 3 forks pre-fix).
                EvmUInt256 maxBlobReservation = EvmUInt256.Zero;
                if (ctx.IsType3Transaction && _config.IntrinsicGasRules.Blob != null)
                {
                    var blobCount = ctx.BlobVersionedHashes?.Count ?? 0;
                    maxBlobReservation = _config.IntrinsicGasRules.Blob
                        .CalculateBlobGasCost(blobCount, ctx.MaxFeePerBlobGas);
                }
                var maxCost = gasCost + ctx.Value + maxBlobReservation;
                var addOverflow = maxCost < gasCost;
                if (addOverflow || senderBalance < maxCost)
                {
                    result.IsValidationError = true;
                    result.Error = $"Insufficient balance: {senderBalance} < {maxCost}";
                    return;
                }
            }

            ctx.SenderNonceBeforeIncrement = (ctx.SenderAccount.Nonce ?? 0UL);

            // Increment the sender nonce BEFORE auth-list processing
            // This is critical for self-sponsored txs where sender = authority
            if (!ctx.IsCallMode)
            {
                if (ctx.SenderNonceBeforeIncrement >= ulong.MaxValue)
                    throw new TransactionValidationException("NONCE_IS_MAX");

                ctx.SenderAccount.Nonce = ctx.SenderNonceBeforeIncrement + 1;
            }

            if (_config.TransactionSetupRules != null)
            {
#if EVM_SYNC
                _config.TransactionSetupRules.ApplyAfterNonceIncrement(ctx, result);
#else
                await _config.TransactionSetupRules.ApplyAfterNonceIncrementAsync(ctx, result);
#endif
                if (result.IsValidationError)
                    return;
            }

            // Gas deduction (after auth list, in canonical order)
            if (!ctx.IsCallMode)
            {
                var gasDeduction = ctx.GasLimit * ctx.EffectiveGasPrice;
                ctx.SenderAccount.Balance.DebitExecutionBalance(gasDeduction);

                if (!ctx.BlobGasCost.IsZero)
                    ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.BlobGasCost);
            }

            ctx.TransactionSnapshotId = ctx.ExecutionState.TakeSnapshot();

            #if EVM_SYNC
            SetupTargetAccount(ctx);
#else
            await SetupTargetAccountAsync(ctx);
#endif
            ProcessAccessList(ctx);
        }

#if EVM_SYNC
        private void SetupTargetAccount(TransactionExecutionContext ctx)
#else
        private async Task SetupTargetAccountAsync(TransactionExecutionContext ctx)
#endif
        {
            ctx.HasCollision = false;

            if (ctx.IsContractCreation)
            {
                ctx.ContractAddress = ContractUtils.CalculateContractAddress(ctx.Sender, (long)ctx.SenderNonceBeforeIncrement);
                ctx.ExecutionState.MarkAddressAsWarm(ctx.ContractAddress);

                #if EVM_SYNC
                var contractCode = ctx.ExecutionState.GetCode(ctx.ContractAddress);
#else
                var contractCode = await ctx.ExecutionState.GetCodeAsync(ctx.ContractAddress);
#endif
                #if EVM_SYNC
                var contractNonce = ctx.ExecutionState.GetNonce(ctx.ContractAddress);
#else
                var contractNonce = await ctx.ExecutionState.GetNonceAsync(ctx.ContractAddress);
#endif
                var contractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);

                var targetHasCode = contractCode != null && contractCode.Length > 0;
                var targetHasNonce = contractNonce > 0;
                var targetHasStorage = contractAccount.Storage != null && contractAccount.Storage.Count > 0;
                ctx.HasCollision = targetHasCode || targetHasNonce || targetHasStorage;

                if (!ctx.HasCollision)
                    ctx.ExecutionState.PrepareNewContractAccount(ctx.ContractAddress, _config.ContractInitialNonce);

                ctx.Code = ctx.HasCollision ? null : ctx.Data;
            }
            else if (!string.IsNullOrEmpty(ctx.To))
            {
                ctx.ExecutionState.MarkAddressAsWarm(ctx.To);
                #if EVM_SYNC
                var targetCode = ctx.ExecutionState.GetCode(ctx.To);
#else
                var targetCode = await ctx.ExecutionState.GetCodeAsync(ctx.To);
#endif

                ctx.Code = targetCode;

                if (_config.TransactionSetupRules != null)
                {
#if EVM_SYNC
                    _config.TransactionSetupRules.ApplyCodeResolution(ctx, null);
#else
                    await _config.TransactionSetupRules.ApplyCodeResolutionAsync(ctx, null);
#endif
                }
            }
        }

#if EVM_SYNC
        private void ExecuteTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecuteTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            result.GasUsed = ctx.IntrinsicGas;
            result.Success = !ctx.HasCollision;
            result.GasRefund = 0;

            if (ctx.HasCollision)
            {
                result.GasUsed = ctx.GasLimit.ToLongSafe();
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.Error = "ADDRESS_COLLISION";
                return;
            }

            if (ctx.Code != null && ctx.Code.Length > 0)
            {
                #if EVM_SYNC
                ExecuteCode(ctx, result);
#else
                await ExecuteCode(ctx, result);
#endif
            }
            else if (!ctx.IsContractCreation && !string.IsNullOrEmpty(ctx.To))
            {
                #if EVM_SYNC
                ExecutePrecompileOrTransfer(ctx, result);
#else
                await ExecutePrecompileOrTransfer(ctx, result);
#endif
            }
            else if (result.Success && !ctx.Value.IsZero)
            {
                ExecuteSimpleTransfer(ctx, result);
            }
            else if (ctx.IsContractCreation && result.Success)
            {
                // CREATE-tx with empty init data + zero value: no code, no
                // transfer — but the contract account is still persisted.
                _config.ContractCreationMaterialiseRule.Apply(ctx, result);
            }
        }

#if EVM_SYNC
        private void ExecuteCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecuteCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            var targetAddress = ctx.IsContractCreation ? ctx.ContractAddress : ctx.To;
            var callContext = new EvmCallContext
            {
                From = ctx.Sender,
                To = targetAddress,
                Data = ctx.IsContractCreation ? new byte[0] : ctx.Data ?? new byte[0],
                Gas = ctx.GasLimit.ToLongSafe() - ctx.IntrinsicGas,
                Value = ctx.Value,
                Nonce = ctx.Nonce,
                GasPrice = ctx.EffectiveGasPrice,
                ChainId = ctx.ChainId
            };

            var programContext = new ProgramContext(
                callContext,
                ctx.ExecutionState,
                null,
                blockNumber: ctx.BlockNumber,
                timestamp: ctx.Timestamp,
                coinbase: ctx.Coinbase,
                baseFee: ctx.BaseFee,
                codeAddress: ctx.IsContractCreation ? ctx.ContractAddress : null
            );

            programContext.Difficulty = ctx.Difficulty;
            programContext.GasLimit = ctx.BlockGasLimit;
            programContext.BlobBaseFee = ctx.BlobBaseFee;

            if (ctx.IsType3Transaction && ctx.BlobVersionedHashes != null && ctx.BlobVersionedHashes.Count > 0)
            {
                var blobHashes = new byte[ctx.BlobVersionedHashes.Count][];
                for (int i = 0; i < ctx.BlobVersionedHashes.Count; i++)
                    blobHashes[i] = ctx.BlobVersionedHashes[i].HexToByteArray();
                programContext.BlobHashes = blobHashes;
            }

            programContext.EnforceGasSentry = _config.EnforceSstoreSentry;
            programContext.BlockHashRule = _config.BlockHashRule;
            programContext.SstoreClearsSchedule = _config.SstoreClearsSchedule;
            programContext.SstoreSetRefund = _config.SstoreSetRefund;
            programContext.SstoreResetRefund = _config.SstoreResetRefund;
            programContext.SstoreRefundRule = _config.SstoreRefundRule;
            // EIP-158+ forks (cleanEmptyAccounts=true) get a permanent
            // touch on revert so end-of-tx cleanup sees the touched-empty set
            // correctly. Pre-EIP-158 forks revert touches normally; no cleanup
            // runs so phantom touches must not leak into post-state.
            ctx.ExecutionState.TouchPersistsOnRevert = _config.CleanEmptyAccounts;

            var program = new Program(ctx.Code, programContext);

            if (!ctx.Value.IsZero)
                ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);

            try
            {
                #if EVM_SYNC
                program = _evmSimulator.ExecuteWithCallStack(program, traceEnabled: ctx.TraceEnabled);
#else
                program = await _evmSimulator.ExecuteWithCallStackAsync(program, traceEnabled: ctx.TraceEnabled);
#endif

                if (ctx.TraceEnabled)
                    result.Traces = program.Trace;

                result.GasUsed = ctx.IntrinsicGas + program.TotalGasUsed;
                result.Success = !program.ProgramResult.IsRevert;
                result.ReturnData = program.ProgramResult.Result;

                if (result.Success)
                {
                    // Store raw refund counter — FinalizeTransaction will combine with AuthRefund
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
                result.Logs = program.ProgramResult.Logs;
                result.InnerCalls = program.ProgramResult.InnerCalls;
                result.InnerContractCodeCalls = program.ProgramResult.InnerContractCodeCalls;

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

                    // HandleSuccessfulContractCreation can flip result.Success
                    // to false on code-deposit OOG (EIP-170 code-size cap, or
                    // GasConstants.CREATE_DATA_GAS exceeding remaining gas) and
                    // reverts the outer tx snapshot. CleanupSelfDestructedContracts
                    // and the CreatedAccounts/DeletedAccounts capture must NOT
                    // run on that path — they'd delete addresses that the
                    // snapshot revert just restored (e.g. a pre-state contract
                    // SELFDESTRUCT'd inside the failed CREATE's init-code).
                    // CreateOOGFromEOARefunds.json [13,0,0] at London/Paris/
                    // Shanghai exercises this exactly: init-code CALLs into
                    // pre-state 0xc0ded which SELFDESTRUCTs; outer CREATE then
                    // OOGs on the 5000*200 code-deposit cost; without the
                    // re-check, the pre-state 0xc0ded is wiped from
                    // AccountsState even though the snapshot revert restored it.
                    // Cancun masks the bug via EIP-6780 (PreCancunSelfDestructRule
                    // only journals SELFDESTRUCT for IsNewContract=true).
                    if (result.Success)
                    {
                        CleanupSelfDestructedContracts(ctx, program);

                        result.CreatedAccounts = new List<string>(program.ProgramResult.CreatedContractAccounts);
                        result.DeletedAccounts = new List<string>(program.ProgramResult.DeletedContractAccounts);
                    }
                }
                else
                {
                    ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                    if (program.GasRemaining == 0)
                        result.GasUsed = ctx.GasLimit.ToLongSafe();
                }
            }
            catch (Exception)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                throw;
            }
        }

#if EVM_SYNC
        private void ExecutePrecompileOrTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
#else
        private async Task ExecutePrecompileOrTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
#endif
        {
            var registry = _config.Precompiles;
            int precompileAddr = -1;
            bool isPrecompile = registry != null
                && TryParsePrecompileAddress(ctx.To, out precompileAddr)
                && registry.CanHandle(precompileAddr);

            if (isPrecompile)
            {
                var precompileInput = ctx.Data ?? new byte[0];
                var precompileGasCost = registry.GetGasCost(precompileAddr, precompileInput);
                var availableGas = ctx.GasLimit.ToLongSafe() - ctx.IntrinsicGas;

                if (precompileGasCost > availableGas)
                {
                    result.GasUsed = ctx.GasLimit.ToLongSafe();
                    result.Success = false;
                    result.Error = "PRECOMPILE_OUT_OF_GAS";
                    ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                }
                else
                {
                    result.GasUsed = ctx.IntrinsicGas + precompileGasCost;
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
                    }
                    catch
                    {
                        result.GasUsed = ctx.GasLimit.ToLongSafe();
                        result.Success = false;
                        result.Error = "PRECOMPILE_FAILED";
                        ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                    }
                }
            }
            else
            {
                // Mirror the canonical CALL's unconditional transfer:
                // a value-zero call still touches the recipient via AddBalance(0),
                // which at EIP-158+ feeds touched-empty cleanup. Skipping the
                // touch leaves empty pre-state recipients in our post-state
                // where the cleanup deletes them. For pre-EIP-158 the touch
                // is a no-op because the cleanup rule is NoOp.
                if (!ctx.Value.IsZero)
                {
                    ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);
                }
                var receiverAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.To);
                receiverAccount.Balance.CreditExecutionBalance(ctx.Value);
                ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            }
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
