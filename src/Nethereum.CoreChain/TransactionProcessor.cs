using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class TransactionProcessor
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly ChainConfig _config;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly TransactionExecutor _executor;
        private readonly HardforkConfig _hardforkConfig;
        private readonly bool _eip158EmptyAccountPruning;
        private readonly Sha3Keccack _keccak = new();

        public const int G_CODEDEPOSIT = 200;

        public TransactionProcessor(
            IStateStore stateStore,
            IBlockStore blockStore,
            ChainConfig config,
            ITransactionVerificationAndRecovery txVerifier,
            HardforkConfig hardforkConfig = null,
            bool eip158EmptyAccountPruning = true)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _txVerifier = txVerifier ?? throw new ArgumentNullException(nameof(txVerifier));
            _hardforkConfig = hardforkConfig ?? config.GetHardforkConfig();
            _eip158EmptyAccountPruning = eip158EmptyAccountPruning;
            _executor = new TransactionExecutor(_hardforkConfig);
        }

        public async Task<TransactionExecutionResult> ExecuteTransactionAsync(
            ISignedTransaction signedTx,
            BlockContext blockContext,
            int txIndex,
            long cumulativeGasUsed,
            string? cachedSenderAddress = null,
            IStateReader stateReader = null,
            bool traceEnabled = false,
            BlockAccessListRecorder? balRecorder = null,
            ulong blockAccessIndex = 0,
            Nethereum.EVM.Gas.BlockGasCapacity? blockGasCapacity = null)
        {
            var result = new TransactionExecutionResult
            {
                Transaction = signedTx,
                TransactionHash = signedTx.Hash,
                TransactionIndex = txIndex
            };

            try
            {
                var senderAddress = cachedSenderAddress ?? _txVerifier.GetSenderAddress(signedTx);
                if (string.IsNullOrEmpty(senderAddress))
                    return RefuseUnrecoverableSender(result);

                result.EffectiveGasPrice = (BigInteger)signedTx.GetEffectiveGasPrice(blockContext.BaseFee);

                var transactionNonce = signedTx.GetNonce();
                var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
                var expectedNonce = senderAccount?.Nonce ?? EvmUInt256.Zero;
                if (expectedNonce != transactionNonce)
                    return SkipForNonceMismatch(result, transactionNonce, expectedNonce, cumulativeGasUsed);

                var snapshot = await _stateStore.CreateSnapshotAsync();

                try
                {
                    var executionState = await OpenExecutionStateAsync(
                        senderAddress, stateReader, balRecorder, blockAccessIndex);

                    var ctx = TransactionContextFactory.From(signedTx, senderAddress, blockContext, executionState);
                    if (blockGasCapacity != null) ctx.BlockGasCapacity = blockGasCapacity;
                    if (traceEnabled) ctx.TraceEnabled = true;

                    var evmResult = await _executor.ExecuteAsync(ctx);

                    if (evmResult.IsValidationError)
                    {
                        await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                        return SkipForValidationError(result, evmResult, cumulativeGasUsed);
                    }

                    await SettleTransactionAsync(snapshot, executionState, balRecorder, evmResult, ctx.To);

                    RecordExecutionOutcome(result, evmResult, signedTx, cumulativeGasUsed, traceEnabled);
                }
                catch (Exception ex) when (Nethereum.EVM.BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
                {
                    await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                    throw;
                }
                catch (Exception ex)
                {
                    await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                    result.RevertReason = ex.Message;
                    ForfeitWholeGasLimit(result, signedTx, cumulativeGasUsed);
                }
            }
            catch (Exception ex) when (Nethereum.EVM.BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Skipped = true;
                result.Success = false;
                result.RevertReason = ex.Message;
                result.GasUsed = 0;
                result.CumulativeGasUsed = cumulativeGasUsed;
            }

            return result;
        }

        public async Task<TransactionExecutionResult> ExecuteSimulatedCallAsync(
            TxEntry entry,
            BlockContext blockContext,
            int txIndex,
            long cumulativeGasUsed,
            IStateReader stateReader,
            SimulateCallOptions simulateOptions,
            BlockAccessListRecorder? balRecorder = null,
            Nethereum.EVM.Gas.BlockGasCapacity? blockGasCapacity = null)
        {
            if (entry.SimulateCall == null)
                throw new ArgumentException(
                    "TxEntry.SimulateCall is required to execute a Simulating-role entry.", nameof(entry));

            var call = entry.SimulateCall;
            var from = entry.CachedSender ?? AddressUtil.ZERO_ADDRESS;

            var result = new TransactionExecutionResult
            {
                Transaction = entry.Tx,
                TransactionHash = entry.Tx.Hash,
                TransactionIndex = txIndex,
                Sender = from
            };

            var snapshot = await _stateStore.CreateSnapshotAsync();

            try
            {
                var executionState = await OpenExecutionStateAsync(
                    from, stateReader, balRecorder, (ulong)(txIndex + 1));

                var isContractCreation = Nethereum.Model.SignedTransactionExtensions.IsContractCreationRecipient(call.To);
                var ctx = BuildSimulateExecutionContext(
                    call, simulateOptions.TraceTransfers, simulateOptions.Validation, from, isContractCreation,
                    call.Gas.Value, blockContext, executionState, simulateOptions.PrecompileRelocations);
                if (blockGasCapacity != null) ctx.BlockGasCapacity = blockGasCapacity;

                if (simulateOptions.Validation)
                {
                    var validationFailure = await ValidateSimulateCallAsync(
                        call, ctx, from, blockContext, executionState, simulateOptions.ExpectedNonces);
                    if (validationFailure != null)
                    {
                        await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                        throw BuildSimulateValidationAbort(validationFailure.Value.Message, validationFailure.Value.Code);
                    }
                }

                var evmResult = await _executor.ExecuteAsync(ctx);

                if (evmResult.IsValidationError)
                {
                    await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                    if (SimulateValidationErrorAborts(simulateOptions.Validation, evmResult.ErrorCode))
                        throw BuildSimulateValidationAbort(evmResult.Error, evmResult.ErrorCode);
                    return SkipForValidationError(result, evmResult, cumulativeGasUsed);
                }

                await SettleTransactionAsync(snapshot, executionState, balRecorder, evmResult, ctx.To);

                RecordExecutionOutcome(result, evmResult, entry.Tx, cumulativeGasUsed, traceEnabled: false);
                ExcludeSimulateTransferLogsFromReceipt(result);
            }
            catch (Exception ex) when (Nethereum.EVM.BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                throw;
            }
            catch (Exception ex) when (!(ex is Nethereum.CoreChain.Rpc.RpcException))
            {
                await DiscardUnitOfWorkAsync(snapshot, balRecorder);
                result.RevertReason = ex.Message;
                ForfeitWholeGasLimit(result, entry.Tx, cumulativeGasUsed);
            }

            return result;
        }

        private static bool SimulateValidationErrorAborts(bool validation, Nethereum.EVM.TransactionError code)
        {
            if (validation) return true;
            return code == Nethereum.EVM.TransactionError.InsufficientBalance
                || code == Nethereum.EVM.TransactionError.IntrinsicGasTooLow;
        }

        private static Nethereum.CoreChain.Rpc.RpcException BuildSimulateValidationAbort(
            string message, Nethereum.EVM.TransactionError code)
        {
            var rpcCode = code switch
            {
                Nethereum.EVM.TransactionError.NonceMismatch => -38010,
                Nethereum.EVM.TransactionError.InsufficientMaxFeePerGas => -38012,
                Nethereum.EVM.TransactionError.IntrinsicGasTooLow => -38013,
                Nethereum.EVM.TransactionError.InsufficientBalance => -38014,
                Nethereum.EVM.TransactionError.NonceIsMax => -32603,
                _ => -32000
            };
            return new Nethereum.CoreChain.Rpc.RpcException(rpcCode, message ?? "simulate call rejected");
        }

        private static TransactionExecutionContext BuildSimulateExecutionContext(
            TransactionInput call, bool traceTransfers, bool validation, string from, bool isContractCreation,
            BigInteger callGasLimit, BlockContext blockContext, ExecutionStateService executionStateService,
            System.Collections.Generic.IReadOnlyDictionary<string, int> precompileRelocations)
        {
            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = isContractCreation ? null : call.To,
                Data = call.Data?.HexToByteArray(),
                Value = call.Value?.Value ?? BigInteger.Zero,
                GasLimit = callGasLimit,
                GasPrice = call.GasPrice?.Value ?? BigInteger.Zero,
                MaxFeePerGas = call.MaxFeePerGas?.Value ?? BigInteger.Zero,
                MaxPriorityFeePerGas = call.MaxPriorityFeePerGas?.Value ?? BigInteger.Zero,
                Nonce = 0,
                IsEip1559 = call.MaxFeePerGas != null,
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                SlotNumber = blockContext.SlotNumber.HasValue
                    ? new EvmUInt256(blockContext.SlotNumber.Value)
                    : EvmUInt256.Zero,
                ExecutionState = executionStateService,
                TraceEnabled = false,
                EthTransferLogRuleOverride = traceTransfers
                    ? Nethereum.EVM.Execution.TransferLogs.Rules.SimulateTraceTransferLogRule.Instance
                    : null,
                EnforceSenderBalance = true,
                SettleTransactionFees = true,
                AllowFeeCapBelowBaseFee = !validation,
                AdvanceSenderNonce = true,
                PreserveZeroBaseFee = true,
                SkipNonceMaxCheck = !validation,
                BlockHashRuleOverride = Nethereum.EVM.Execution.Opcodes.Executors.Rules.SimulateBlockHashRule.Instance,
                PrecompileRelocations = precompileRelocations
            };
        }

        private static async Task<(string Message, TransactionError Code)?> ValidateSimulateCallAsync(
            TransactionInput call, TransactionExecutionContext ctx, string from, BlockContext blockContext,
            ExecutionStateService executionStateService, Dictionary<string, EvmUInt256> expectedNonces)
        {
            var effectiveMaxFee = ctx.IsEip1559 ? ctx.MaxFeePerGas : ctx.GasPrice;

            if (effectiveMaxFee < (EvmUInt256)blockContext.BaseFee)
                return ("max fee per gas less than block base fee", TransactionError.InsufficientMaxFeePerGas);

            var requiredFunds = ctx.Value + ctx.GasLimit * effectiveMaxFee;
            var senderBalance = await executionStateService.GetTotalBalanceAsync(from);
            if (senderBalance < requiredFunds)
                return ("insufficient funds for gas * price + value", TransactionError.InsufficientBalance);

            var expectedNonce = await ResolveExpectedNonceAsync(from, executionStateService, expectedNonces);
            if (call.Nonce != null && (EvmUInt256)call.Nonce.Value != expectedNonce)
                return ($"invalid nonce: expected {(BigInteger)expectedNonce}, got {call.Nonce.Value}", TransactionError.NonceMismatch);

            ctx.Nonce = expectedNonce;
            expectedNonces[from.ToLower()] = expectedNonce + EvmUInt256.One;
            return null;
        }

        private static async Task<EvmUInt256> ResolveExpectedNonceAsync(
            string from, ExecutionStateService executionStateService, Dictionary<string, EvmUInt256> expectedNonces)
        {
            var key = from.ToLower();
            if (expectedNonces.TryGetValue(key, out var expected))
                return expected;

            return await executionStateService.GetNonceAsync(from);
        }

        private static TransactionExecutionResult RefuseUnrecoverableSender(TransactionExecutionResult result)
        {
            result.Success = false;
            result.RevertReason = "Invalid signature: cannot recover sender address";
            return result;
        }

        private static TransactionExecutionResult SkipForNonceMismatch(
            TransactionExecutionResult result,
            EvmUInt256 transactionNonce,
            EvmUInt256 expectedNonce,
            long cumulativeGasUsed)
        {
            result.Skipped = true;
            result.Success = false;
            result.RevertReason = $"Nonce mismatch: have {transactionNonce}, expected {expectedNonce}";
            result.ErrorCode = TransactionError.NonceMismatch;
            result.GasUsed = 0;
            result.CumulativeGasUsed = cumulativeGasUsed;
            return result;
        }

        private static TransactionExecutionResult SkipForValidationError(
            TransactionExecutionResult result,
            Nethereum.EVM.TransactionExecutionResult evmResult,
            long cumulativeGasUsed)
        {
            result.Skipped = true;
            result.Success = false;
            result.RevertReason = evmResult.Error;
            result.ErrorCode = evmResult.ErrorCode;
            result.GasUsed = 0;
            result.CumulativeGasUsed = cumulativeGasUsed;
            return result;
        }

        private async Task<ExecutionStateService> OpenExecutionStateAsync(
            string senderAddress,
            IStateReader stateReader,
            BlockAccessListRecorder? balRecorder,
            ulong blockAccessIndex)
        {
            IStateReader nodeDataService = stateReader ?? new StateStoreNodeDataService(_stateStore, _blockStore);
            var executionState = new ExecutionStateService(nodeDataService);

            if (balRecorder != null)
            {
                balRecorder.BeginUnit(blockAccessIndex);
                executionState.AccessRecorder = balRecorder;
            }

            var senderBalance = await nodeDataService.GetBalanceAsync(senderAddress);
            executionState.SetInitialChainBalance(senderAddress, senderBalance);

            return executionState;
        }

        private async Task DiscardUnitOfWorkAsync(
            Storage.IStateSnapshot snapshot, BlockAccessListRecorder? balRecorder)
        {
            await _stateStore.RevertSnapshotAsync(snapshot);
            balRecorder?.DiscardUnit();
        }

        /// <summary>
        /// Runs one EIP-7928 recording step (any of <c>BlockAccessListRecorder</c>'s
        /// Prepare*/Record* methods) and forces any fault it raises through the host-fault path.
        /// Every call site that drives the recorder — the per-transaction loop, the EIP-7685
        /// request system calls, the EIP-4788/2935 pre-transaction system calls, and withdrawal
        /// crediting — routes through this, so the guarantee is uniform rather than true only
        /// where a reviewer happened to look.
        ///
        /// <para>The specific danger this closes is narrower at some call sites than others.
        /// <see cref="ExecuteTransactionAsync"/>'s generic catch converts ANY exception into a
        /// forfeit-all-gas reverted receipt — without this wrapper, a fault here (a transient
        /// state-store read failure, or the recorder's own malformed-nonce guard) would record a
        /// transaction whose EVM execution actually SUCCEEDED as reverted. The other call sites
        /// (system calls, withdrawals) have no such absorbing catch — a raw fault there already
        /// propagates to <c>BlockExecutor.ExecuteAsync</c>'s outer catch and halts the block
        /// loudly regardless. Wrapping them anyway buys the second half of the contract: the
        /// exception that halts the block is phase-labelled and classified as a host fault
        /// instead of an untyped one, which is the difference between fast and slow triage when a
        /// node halts on a store read that nobody expected to fail. The access list is a record OF
        /// execution; it must never be able to change what execution did, and a fault while
        /// recording it should never look like anything other than exactly what it is.</para>
        ///
        /// <para>A fault that is already classified as a host/system fault
        /// (<see cref="EvmHostException"/>, cancellation, OOM) is rethrown unchanged — it already takes
        /// the right path through <see cref="EvmHostException.IsHostOrSystemFault"/> at the call site's
        /// catch clauses. Anything else derives an <see cref="EvmHostException"/> from it, exactly the
        /// case that exception type's own doc comment describes ("a wired state backend's own IO
        /// exceptions can derive from this to opt into the same treatment") — this does not widen that
        /// filter, it makes an existing fault visible to it.</para>
        /// </summary>
        internal static async Task RunBalRecordingAsync(Func<Task> recordingStep, string phase)
        {
            try
            {
                await recordingStep().ConfigureAwait(false);
            }
            catch (Exception ex) when (!EvmHostException.IsHostOrSystemFault(ex))
            {
                throw new EvmHostException(
                    $"EIP-7928 block-access-list recording faulted while {phase} — treated as a host fault.",
                    ex);
            }
        }

        private void RecordExecutionOutcome(
            TransactionExecutionResult result,
            Nethereum.EVM.TransactionExecutionResult evmResult,
            ISignedTransaction signedTx,
            long cumulativeGasUsed,
            bool traceEnabled)
        {
            result.Success = evmResult.Success;
            result.GasUsed = evmResult.GasUsed;
            result.CumulativeGasUsed = cumulativeGasUsed + evmResult.GasUsed;
            result.ExecutionGasUsed = evmResult.ExecutionGasUsed;
            result.StateGasUsed = evmResult.StateGasUsed;
            result.GasRefund = evmResult.GasRefund;
            result.ReturnData = evmResult.ReturnData;
            result.IsRevert = evmResult.ProgramResult?.IsRevert ?? false;
            result.IsOutOfGas = !evmResult.Success && (evmResult.Program?.IsExceptionalHalt ?? false);
            result.RevertReason = evmResult.RevertReason ?? evmResult.Error;
            result.ContractAddress = evmResult.ContractAddress;
            if (traceEnabled) result.Traces = evmResult.Traces;

            result.Logs = evmResult.Success ? ConvertLogs(evmResult.Logs) : new List<Log>();

            ConstructReceipt(result, signedTx, evmResult.Success, CalculateLogsBloom(result.Logs), result.Logs);
        }

        private void ForfeitWholeGasLimit(
            TransactionExecutionResult result, ISignedTransaction signedTx, long cumulativeGasUsed)
        {
            var gasLimit = signedTx.GetGasLimit();

            result.Success = false;
            result.GasUsed = gasLimit;
            result.CumulativeGasUsed = cumulativeGasUsed + gasLimit;
            result.ExecutionGasUsed = (long)gasLimit;

            ConstructReceipt(result, signedTx, succeeded: false, bloom: new byte[256], logs: new List<Log>());
        }

        private void ExcludeSimulateTransferLogsFromReceipt(TransactionExecutionResult result)
        {
            if (result.Logs == null || result.Logs.Count == 0) return;

            var receiptLogs = new List<Log>(result.Logs.Count);
            foreach (var log in result.Logs)
            {
                if (log.Address != null && log.Address.IsTheSameAddress(
                        Nethereum.EVM.Execution.TransferLogs.Rules.SimulateTraceTransferLogRule.TraceTransferAddress))
                    continue;
                receiptLogs.Add(log);
            }

            if (receiptLogs.Count == result.Logs.Count) return;

            ConstructReceipt(result, result.Transaction, result.Success, CalculateLogsBloom(receiptLogs), receiptLogs);
        }

        private void ConstructReceipt(
            TransactionExecutionResult result, ISignedTransaction signedTx,
            bool succeeded, byte[] bloom, List<Log> logs)
        {
            result.Receipt = _hardforkConfig.ReceiptConstruction.Construct(
                succeeded, result.CumulativeGasUsed, bloom, logs, intermediatePostStateRoot: null);
            result.Receipt.TransactionType = signedTx.TransactionType.AsChainByteType();
        }

        private async Task MaterialisePreEip158EmptyAccountAsync(
            Nethereum.EVM.TransactionExecutionResult evmResult, string recipient)
        {
            if (_eip158EmptyAccountPruning || !evmResult.Success) return;

            var address = string.IsNullOrEmpty(evmResult.ContractAddress)
                ? recipient
                : evmResult.ContractAddress;

            if (string.IsNullOrEmpty(address)) return;
            if (WasRemovedThisTx(evmResult.DeletedAccounts, address)) return;
            if (await _stateStore.GetAccountAsync(address) != null) return;

            await _stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = EvmUInt256.Zero,
                Balance = EvmUInt256.Zero,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            });
        }

        private async Task PersistExecutionStateChangesAsync(ExecutionStateService executionStateService)
        {
            foreach (var accountKvp in executionStateService.AccountsState)
            {
                var address = accountKvp.Key;
                await PersistDirtyStorageAsync(address, accountKvp.Value);
                await PersistAccountFieldsAsync(address, accountKvp.Value);
            }
        }

        private async Task PersistDirtyStorageAsync(EvmAddress address, AccountExecutionState accountState)
        {
            foreach (var storageKvp in accountState.Storage)
            {
                var slot = storageKvp.Key;
                var value = storageKvp.Value;
                if (accountState.OriginalStorageValues.TryGetValue(slot, out var original)
                    && ByteUtil.AreEqual(original ?? Array.Empty<byte>(), value ?? Array.Empty<byte>()))
                {
                    continue;
                }
                await _stateStore.SaveStorageAsync(address, slot, value);
            }
        }

        private async Task PersistAccountFieldsAsync(EvmAddress address, AccountExecutionState accountState)
        {
            var existingAccount = await _stateStore.GetAccountAsync(address);
            var account = existingAccount ?? new Account
            {
                Balance = 0,
                Nonce = 0,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH
            };

            var needsSave = KeepsTouchedEmptyAccountBeforeEip158(existingAccount, accountState);
            needsSave |= await SyncCodeAsync(account, accountState);
            needsSave |= SyncBalance(account, accountState);
            needsSave |= SyncNonce(account, accountState);

            if (needsSave)
            {
                await _stateStore.SaveAccountAsync(address, account);
            }
        }

        private bool KeepsTouchedEmptyAccountBeforeEip158(Account existingAccount, AccountExecutionState accountState)
        {
            return !_eip158EmptyAccountPruning && existingAccount == null && accountState.IsTouched;
        }

        private async Task<bool> SyncCodeAsync(Account account, AccountExecutionState accountState)
        {
            if (accountState.Code == null) return false;

            if (accountState.Code.Length == 0)
            {
                if (IsEmptyCodeHash(account.CodeHash)) return false;
                account.CodeHash = DefaultValues.EMPTY_DATA_HASH;
                return true;
            }

            var codeHash = _keccak.CalculateHash(accountState.Code);
            if (!IsEmptyCodeHash(account.CodeHash) && ByteUtil.AreEqual(account.CodeHash, codeHash)) return false;

            await _stateStore.SaveCodeAsync(codeHash, accountState.Code);
            account.CodeHash = codeHash;
            return true;
        }

        private static bool SyncBalance(Account account, AccountExecutionState accountState)
        {
            if (!accountState.Balance.InitialChainBalance.HasValue
                && !accountState.Balance.ExecutionBalance.HasValue) return false;

            var newBalance = accountState.Balance.InitialChainBalance.HasValue
                ? accountState.Balance.GetTotalBalance()
                : account.Balance + accountState.Balance.ExecutionBalance.Value;

            if (account.Balance == newBalance) return false;
            account.Balance = newBalance;
            return true;
        }

        private static bool SyncNonce(Account account, AccountExecutionState accountState)
        {
            if (!accountState.Nonce.HasValue || account.Nonce == accountState.Nonce.Value) return false;
            account.Nonce = accountState.Nonce.Value;
            return true;
        }

        public async Task<byte[]> ExecuteSystemCallAsync(
            string targetAddress,
            BlockContext blockContext,
            IStateReader stateReader = null,
            BlockAccessListRecorder? balRecorder = null,
            ulong blockAccessIndex = 0,
            byte[] callData = null)
        {
            if (string.IsNullOrEmpty(targetAddress)) throw new ArgumentException("targetAddress required", nameof(targetAddress));
            if (blockContext == null) throw new ArgumentNullException(nameof(blockContext));

            var snapshot = await _stateStore.CreateSnapshotAsync();

            try
            {
                var executionState = await OpenSystemCallStateAsync(stateReader, balRecorder, blockAccessIndex);

                var targetCode = await executionState.GetCodeAsync(targetAddress);
                if (SystemCallExecution.IsAbsent(targetCode))
                {
                    SystemCallExecution.RefuseBlockOnAbsentRequestPredeploy(targetAddress);
                }

                var evmResult = await _executor.ExecuteAsync(
                    BuildSystemCallContext(targetAddress, blockContext, callData, executionState));

                RefuseBlockOnFatalCallFailure(targetAddress, evmResult);

                await SettleSystemCallAsync(snapshot, executionState, balRecorder, evmResult);

                return evmResult.ReturnData ?? Array.Empty<byte>();
            }
            catch
            {
                await _stateStore.RevertSnapshotAsync(snapshot);
                balRecorder?.DiscardUnit();
                throw;
            }
        }

        private async Task<ExecutionStateService> OpenSystemCallStateAsync(
            IStateReader stateReader,
            BlockAccessListRecorder? balRecorder,
            ulong blockAccessIndex)
        {
            IStateReader nodeDataService = stateReader ?? new StateStoreNodeDataService(_stateStore, _blockStore);
            var executionState = new ExecutionStateService(nodeDataService);

            if (balRecorder != null)
            {
                balRecorder.BeginUnit(blockAccessIndex);
                executionState.AccessRecorder = balRecorder;
            }

            var senderAddress = Forks.Eip7685Constants.SystemAddress;
            executionState.SetInitialChainBalance(
                senderAddress, await nodeDataService.GetBalanceAsync(senderAddress));

            return executionState;
        }

        private static TransactionExecutionContext BuildSystemCallContext(
            string targetAddress,
            BlockContext blockContext,
            byte[] callData,
            ExecutionStateService executionState) =>
            new TransactionExecutionContext
            {
                Sender = Forks.Eip7685Constants.SystemAddress,
                To = targetAddress,
                Data = callData ?? Array.Empty<byte>(),
                Value = EvmUInt256.Zero,
                GasPrice = EvmUInt256.Zero,
                MaxFeePerGas = EvmUInt256.Zero,
                MaxPriorityFeePerGas = BigInteger.Zero,
                Nonce = EvmUInt256.Zero,
                IsEip1559 = false,
                IsContractCreation = false,
                Mode = ExecutionMode.SystemCall,
                BlockNumber = EvmUInt256.FromHeaderScalar((long)blockContext.BlockNumber),
                Timestamp = EvmUInt256.FromHeaderScalar(blockContext.Timestamp),
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                ExecutionState = executionState,
                TraceEnabled = false,
                AccessList = null,
                AuthorisationList = null
            };

        private static void RefuseBlockOnFatalCallFailure(string targetAddress, Nethereum.EVM.TransactionExecutionResult evmResult)
        {
            if (evmResult.Success && !evmResult.IsValidationError) return;
            if (!Nethereum.EVM.Execution.SystemCallFailurePolicy.FailureInvalidatesBlock(targetAddress)) return;

            throw new Nethereum.EVM.Execution.SystemCallFailedException(
                targetAddress, evmResult.Error ?? evmResult.RevertReason ?? "unknown");
        }

        private async Task SettleSystemCallAsync(
            Storage.IStateSnapshot snapshot,
            ExecutionStateService executionState,
            BlockAccessListRecorder? balRecorder,
            Nethereum.EVM.TransactionExecutionResult evmResult)
        {
            await CaptureUnitOfWorkBasisAsync(balRecorder, executionState, SystemCallUnitOfWork);
            await PersistExecutionStateChangesAsync(executionState);
            await RemoveAccountsDeletedFromStateAsync(evmResult.DeletedAccounts);
            await CommitAndRecordUnitOfWorkAsync(snapshot, executionState, balRecorder, SystemCallUnitOfWork);
        }

        private async Task SettleTransactionAsync(
            Storage.IStateSnapshot snapshot,
            ExecutionStateService executionState,
            BlockAccessListRecorder? balRecorder,
            Nethereum.EVM.TransactionExecutionResult evmResult,
            string recipient)
        {
            await CaptureUnitOfWorkBasisAsync(balRecorder, executionState, TransactionUnitOfWork);
            await PersistExecutionStateChangesAsync(executionState);
            await RemoveAccountsDeletedFromStateAsync(evmResult.DeletedAccounts);
            await MaterialisePreEip158EmptyAccountAsync(evmResult, recipient);
            await CommitAndRecordUnitOfWorkAsync(snapshot, executionState, balRecorder, TransactionUnitOfWork);
        }

        private const string TransactionUnitOfWork = "(transaction)";
        private const string SystemCallUnitOfWork = "(system call)";

        private Task CaptureUnitOfWorkBasisAsync(
            BlockAccessListRecorder? balRecorder, ExecutionStateService executionState, string unitOfWork)
        {
            if (balRecorder == null) return Task.CompletedTask;
            return RunBalRecordingAsync(
                () => balRecorder.PrepareUnitOfWorkAsync(executionState), "preparing " + unitOfWork);
        }

        private async Task CommitAndRecordUnitOfWorkAsync(
            Storage.IStateSnapshot snapshot,
            ExecutionStateService executionState,
            BlockAccessListRecorder? balRecorder,
            string unitOfWork)
        {
            await _stateStore.CommitSnapshotAsync(snapshot);

            if (balRecorder != null)
                await RunBalRecordingAsync(
                    () => balRecorder.RecordUnitOfWorkAsync(executionState), "recording " + unitOfWork).ConfigureAwait(false);
        }

        private async Task RemoveAccountsDeletedFromStateAsync(IEnumerable<string> deletedAccounts)
        {
            if (deletedAccounts == null) return;

            foreach (var deletedAddress in deletedAccounts)
            {
                if (string.IsNullOrEmpty(deletedAddress)) continue;
                await _stateStore.ClearStorageAsync(deletedAddress);
                await _stateStore.DeleteAccountAsync(deletedAddress);
            }
        }

        private List<Log> ConvertLogs(List<FilterLog> filterLogs)
        {
            var logs = new List<Log>();
            foreach (var fl in filterLogs)
            {
                var topics = new List<byte[]>();
                if (fl.Topics != null)
                {
                    foreach (var topic in fl.Topics)
                    {
                        if (topic is string topicStr)
                        {
                            topics.Add(topicStr.HexToByteArray());
                        }
                        else if (topic is byte[] topicBytes)
                        {
                            topics.Add(topicBytes);
                        }
                    }
                }

                logs.Add(new Log
                {
                    Address = fl.Address,
                    Data = fl.Data?.HexToByteArray() ?? new byte[0],
                    Topics = topics
                });
            }
            return logs;
        }

        private byte[] CalculateLogsBloom(List<Log> logs)
        {
            var bloom = new byte[256];

            foreach (var log in logs)
            {
                AddToBloom(bloom, log.Address.HexToByteArray());
                foreach (var topic in log.Topics)
                {
                    AddToBloom(bloom, topic);
                }
            }

            return bloom;
        }

        private void AddToBloom(byte[] bloom, byte[] data)
        {
            var hash = _keccak.CalculateHash(data);

            for (int i = 0; i < 6; i += 2)
            {
                var bit = ((hash[i] & 0x07) << 8) + hash[i + 1];
                bit = bit & 0x7FF;
                var byteIndex = 255 - (bit / 8);
                var bitIndex = bit % 8;
                bloom[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        private static bool WasRemovedThisTx(List<string> deletedAccounts, string address)
        {
            if (deletedAccounts == null || string.IsNullOrEmpty(address)) return false;
            for (int i = 0; i < deletedAccounts.Count; i++)
            {
                if (string.Equals(deletedAccounts[i], address, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsEmptyCodeHash(byte[] codeHash)
        {
            if (codeHash == null) return true;
            if (codeHash.Length != DefaultValues.EMPTY_DATA_HASH.Length) return false;
            for (int i = 0; i < codeHash.Length; i++)
            {
                if (codeHash[i] != DefaultValues.EMPTY_DATA_HASH[i]) return false;
            }
            return true;
        }
    }
}
