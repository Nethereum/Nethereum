using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
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

        public const int G_TRANSACTION = 21000;
        public const int G_TXDATAZERO = 4;
        public const int G_TXDATANONZERO = 16;
        public const int G_TXCREATE = 32000;
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
            // EIP-158 (Spurious Dragon) prunes empty accounts at the end of a
            // transaction. Pre-EIP-158 (Frontier through Tangerine Whistle), a
            // successful CREATE whose init code leaves the new contract empty
            // STILL records it in state. Default = true (modern behaviour).
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
            bool traceEnabled = false)
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
                {
                    result.Success = false;
                    result.RevertReason = "Invalid signature: cannot recover sender address";
                    return result;
                }

                var txData = GetTransactionData(signedTx);

                // Effective gas price = the per-gas price the sender actually
                // paid. For legacy / EIP-2930 = gasPrice. For EIP-1559+ =
                // baseFee + min(maxPriority, maxFee - baseFee). Stamp it on
                // the result so archive-mode receipt persistence
                // (BlockImporter → IReceiptStore) records the right value.
                result.EffectiveGasPrice = (BigInteger)txData.GetEffectiveGasPrice(blockContext.BaseFee);

                var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
                var expectedNonce = senderAccount?.Nonce ?? EvmUInt256.Zero;
                if (expectedNonce != txData.Nonce)
                {
                    result.Skipped = true;
                    result.Success = false;
                    result.RevertReason = $"Nonce mismatch: have {txData.Nonce}, expected {expectedNonce}";
                    result.GasUsed = 0;
                    result.CumulativeGasUsed = cumulativeGasUsed;
                    return result;
                }

                var snapshot = await _stateStore.CreateSnapshotAsync();

                try
                {
                    IStateReader nodeDataService = stateReader ?? new StateStoreNodeDataService(_stateStore, _blockStore);
                    var executionStateService = new ExecutionStateService(nodeDataService);

                    var senderBalance = await nodeDataService.GetBalanceAsync(senderAddress);
                    executionStateService.SetInitialChainBalance(senderAddress, senderBalance);

                    var ctx = BuildExecutionContext(txData, senderAddress, blockContext, executionStateService);
                    if (traceEnabled) ctx.TraceEnabled = true;

                    var evmResult = await _executor.ExecuteAsync(ctx);

                    if (evmResult.IsValidationError)
                    {
                        await _stateStore.RevertSnapshotAsync(snapshot);
                        result.Skipped = true;
                        result.Success = false;
                        result.RevertReason = evmResult.Error;
                        result.GasUsed = 0;
                        result.CumulativeGasUsed = cumulativeGasUsed;
                        return result;
                    }

                    await PersistExecutionStateChangesAsync(executionStateService);

                    // SELFDESTRUCT cleanup: the executor removes destructed
                    // contracts from ExecutionStateService.AccountsState
                    // (in-memory only). The on-disk leaf in IStateStore stays
                    // behind unless we explicitly delete it here. Without this,
                    // the persisted state trie diverges from the canonical one,
                    // which drops destructed objects during finalisation.
                    // Exposed by mainnet block 51,921 (Greeter.kill() SUICIDE
                    // to owner on a pre-EIP-158 Frontier block).
                    // Fork-blind: DeletedAccounts is only populated by the
                    // active SelfDestructRule, which already encodes the
                    // pre/post-Cancun semantics (Cancun EIP-6780 only marks
                    // same-tx-create SELFDESTRUCTs for deletion).
                    if (evmResult.Success && evmResult.DeletedAccounts != null)
                    {
                        foreach (var deletedAddr in evmResult.DeletedAccounts)
                        {
                            if (string.IsNullOrEmpty(deletedAddr)) continue;
                            await _stateStore.ClearStorageAsync(deletedAddr);
                            await _stateStore.DeleteAccountAsync(deletedAddr);
                        }
                    }

                    // Pre-EIP-158: ensure that a successful CREATE which left
                    // the new contract with no code, no storage, no balance and
                    // nonce 0 is still recorded in state. The execution-state
                    // dictionary skips such inert accounts because nothing
                    // touched them — but consensus says they exist.
                    if (!_eip158EmptyAccountPruning
                        && evmResult.Success
                        && !string.IsNullOrEmpty(evmResult.ContractAddress)
                        && !WasSelfDestructedThisTx(evmResult.DeletedAccounts, evmResult.ContractAddress))
                    {
                        var existing = await _stateStore.GetAccountAsync(evmResult.ContractAddress);
                        if (existing == null)
                        {
                            await _stateStore.SaveAccountAsync(evmResult.ContractAddress, new Account
                            {
                                Nonce = EvmUInt256.Zero,
                                Balance = EvmUInt256.Zero,
                                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                                CodeHash = DefaultValues.EMPTY_DATA_HASH
                            });
                        }
                    }

                    // Pre-EIP-158: a tx to a non-existent EOA address creates the
                    // recipient as an empty account (nonce=0, balance=0,
                    // codeHash=EMPTY_DATA_HASH). Yellow Paper §7 substate Λ
                    // includes the destination address regardless of value/data.
                    // The EVM doesn't touch the recipient when value=0 and
                    // data=0 (zero-cost call), so PersistExecutionStateChanges
                    // skips it — we must materialise it here. First mainnet
                    // hit: block 46,383 (tx to 0x7a19…). EIP-158 (Spurious Dragon,
                    // block 2,675,000) reversed this: empty touched accounts
                    // are NOT created/persisted.
                    if (!_eip158EmptyAccountPruning
                        && evmResult.Success
                        && string.IsNullOrEmpty(evmResult.ContractAddress)
                        && !string.IsNullOrEmpty(txData.To)
                        && !WasSelfDestructedThisTx(evmResult.DeletedAccounts, txData.To))
                    {
                        var existing = await _stateStore.GetAccountAsync(txData.To);
                        if (existing == null)
                        {
                            await _stateStore.SaveAccountAsync(txData.To, new Account
                            {
                                Nonce = EvmUInt256.Zero,
                                Balance = EvmUInt256.Zero,
                                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                                CodeHash = DefaultValues.EMPTY_DATA_HASH
                            });
                        }
                    }

                    await _stateStore.CommitSnapshotAsync(snapshot);

                    result.Success = evmResult.Success;
                    result.GasUsed = evmResult.GasUsed;
                    result.CumulativeGasUsed = cumulativeGasUsed + evmResult.GasUsed;
                    result.ReturnData = evmResult.ReturnData;
                    result.RevertReason = evmResult.RevertReason ?? evmResult.Error;
                    result.ContractAddress = evmResult.ContractAddress;
                    if (traceEnabled) result.Traces = evmResult.Traces;

                    if (evmResult.Success)
                    {
                        result.Logs = ConvertLogs(evmResult.Logs);
                    }
                    else
                    {
                        result.Logs = new List<Log>();
                    }

                    var bloom = CalculateLogsBloom(result.Logs);
                    result.Receipt = _hardforkConfig.ReceiptConstruction.Construct(
                        evmResult.Success,
                        result.CumulativeGasUsed,
                        bloom,
                        result.Logs,
                        intermediatePostStateRoot: null);
                    result.Receipt.TransactionType = GetTransactionType(signedTx);
                }
                catch (Exception ex)
                {
                    await _stateStore.RevertSnapshotAsync(snapshot);
                    result.Success = false;
                    result.RevertReason = ex.Message;
                    result.GasUsed = txData.GasLimit;
                    result.CumulativeGasUsed = cumulativeGasUsed + txData.GasLimit;
                    result.Receipt = _hardforkConfig.ReceiptConstruction.Construct(
                        false, result.CumulativeGasUsed, new byte[256], new List<Log>(), intermediatePostStateRoot: null);
                    result.Receipt.TransactionType = GetTransactionType(signedTx);
                }
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

        private TransactionExecutionContext BuildExecutionContext(
            TransactionData txData,
            string senderAddress,
            BlockContext blockContext,
            ExecutionStateService executionState)
        {
            var isContractCreation = string.IsNullOrEmpty(txData.To) || txData.To == "0x";

            return new TransactionExecutionContext
            {
                Sender = senderAddress,
                To = isContractCreation ? null : txData.To,
                Data = txData.Data,
                Value = txData.Value,
                GasLimit = txData.GasLimit,
                GasPrice = txData.GasPrice,
                MaxFeePerGas = txData.MaxFeePerGas ?? txData.GasPrice,
                MaxPriorityFeePerGas = txData.MaxPriorityFeePerGas ?? BigInteger.Zero,
                Nonce = txData.Nonce,
                IsEip1559 = txData.MaxFeePerGas.HasValue,
                IsType3Transaction = txData.BlobVersionedHashes != null && txData.BlobVersionedHashes.Count > 0,
                IsType4Transaction = txData.AuthorisationList != null && txData.AuthorisationList.Count > 0,
                IsContractCreation = isContractCreation,
                BlobVersionedHashes = txData.BlobVersionedHashes,
                MaxFeePerBlobGas = txData.MaxFeePerBlobGas ?? EvmUInt256.Zero,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                ExecutionState = executionState,
                TraceEnabled = false,
                AccessList = txData.AccessList,
                AuthorisationList = txData.AuthorisationList,
                ExcessBlobGas = (EvmUInt256)(ulong)blockContext.ExcessBlobGas
            };
        }

        private async Task PersistExecutionStateChangesAsync(ExecutionStateService executionStateService)
        {
            foreach (var accountKvp in executionStateService.AccountsState)
            {
                var address = accountKvp.Key;
                var accountState = accountKvp.Value;

                // Only persist slots that ACTUALLY changed during execution.
                // TrackAndWriteStorage (SLOAD-warm) and SetPreStateStorage
                // (pre-state load) both populate OriginalStorageValues[slot]
                // = value AND Storage[slot] = value with the same value.
                // SSTORE (UpsertStorageValue) updates Storage[slot] but
                // leaves OriginalStorageValues[slot] at the pre-tx value.
                // Persisting SLOAD-only slots re-dirties the contract in
                // the trie tracker, which then re-encodes the contract's
                // leaf with a (potentially stale) cached storage root —
                // corrupting state. Block 1,149,150 surfaced this live
                // (SLOAD-heavy contract call → state-root divergence).
                // The canonical state-finalisation rule has the same "only
                // write back dirty storage" invariant.
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

                var existingAccount = await _stateStore.GetAccountAsync(address);
                var account = existingAccount ?? new Account
                {
                    Balance = 0,
                    Nonce = 0,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH
                };

                var needsSave = false;

                // Pre-EIP-158 (Frontier/Homestead/TangerineWhistle): a touched
                // empty account must persist in the state trie. Canonical
                // finalisation with deleteEmptyObjects:false keeps every dirty
                // account regardless of emptiness. CALL with
                // value=0 to a precompile (e.g. 0x04 via the IDENTITY-as-memcpy
                // Solidity pattern) does AddBalance(addr, 0) → touches the
                // precompile address. At Frontier the precompile entry then
                // appears in the canonical state-trie even though all of its
                // fields are zero.
                // Without this, mainnet block 49,439 (Greeter.greet() Solidity
                // pattern) diverges from canonical: our trie misses the
                // touched 0x...04 entry. Symmetric to the existing pre-EIP-158
                // recipient creation carve-out at lines 144-170 (which only
                // handles tx-level recipients, not intra-execution touches).
                if (!_eip158EmptyAccountPruning && existingAccount == null && accountState.IsTouched)
                {
                    needsSave = true;
                }

                if (accountState.Code != null)
                {
                    if (accountState.Code.Length > 0 && IsEmptyCodeHash(account.CodeHash))
                    {
                        var codeHash = _keccak.CalculateHash(accountState.Code);
                        await _stateStore.SaveCodeAsync(codeHash, accountState.Code);
                        account.CodeHash = codeHash;
                        needsSave = true;
                    }
                    else if (accountState.Code.Length > 0 && !IsEmptyCodeHash(account.CodeHash))
                    {
                        var newCodeHash = _keccak.CalculateHash(accountState.Code);
                        if (!ByteUtil.AreEqual(account.CodeHash, newCodeHash))
                        {
                            await _stateStore.SaveCodeAsync(newCodeHash, accountState.Code);
                            account.CodeHash = newCodeHash;
                            needsSave = true;
                        }
                    }
                    else if (accountState.Code.Length == 0 && !IsEmptyCodeHash(account.CodeHash))
                    {
                        account.CodeHash = DefaultValues.EMPTY_DATA_HASH;
                        needsSave = true;
                    }
                }

                // Only update balance if it was actually accessed during execution
                // (InitialChainBalance being set indicates balance was queried/modified)
                if (accountState.Balance.InitialChainBalance.HasValue || accountState.Balance.ExecutionBalance.HasValue)
                {
                    EvmUInt256 newBalance;
                    if (accountState.Balance.ExecutionBalance.HasValue && !accountState.Balance.InitialChainBalance.HasValue)
                    {
                        // ExecutionBalance was modified but InitialChainBalance was never loaded.
                        // This happens when a contract is called (its code accessed) but its balance wasn't queried.
                        // Use the existing account balance as the base and add the execution delta.
                        newBalance = account.Balance + accountState.Balance.ExecutionBalance.Value;
                    }
                    else
                    {
                        newBalance = accountState.Balance.GetTotalBalance();
                    }

                    if (account.Balance != newBalance)
                    {
                        account.Balance = newBalance;
                        needsSave = true;
                    }
                }

                if (accountState.Nonce.HasValue && account.Nonce != accountState.Nonce.Value)
                {
                    account.Nonce = accountState.Nonce.Value;
                    needsSave = true;
                }

                if (needsSave)
                {
                    await _stateStore.SaveAccountAsync(address, account);
                }
            }
        }

        /// <summary>
        /// Execute a post-Prague end-of-block system call against a predeploy
        /// (EIP-7002 withdrawal queue, EIP-7251 consolidation queue, EIP-2935
        /// history contract, etc.). The call is made by
        /// <see cref="Forks.Eip7685Constants.SystemAddress"/> with empty
        /// calldata and a fixed 30M-gas budget per EIP. The predeploy mutates
        /// its own ring-buffer storage to dequeue serviced requests; we
        /// persist those mutations via the same path as the per-tx loop. Per
        /// EIP-7685 a failure of the system call is a consensus error — the
        /// block is invalid — so we surface the error to the caller rather
        /// than silently no-op.
        /// </summary>
        /// <param name="targetAddress">Predeploy address.</param>
        /// <param name="blockContext">Block environment, identical to the
        /// one used for the tx loop.</param>
        /// <param name="stateReader">Optional witness-recording reader so
        /// the predeploy's account + code + slot reads enter the witness.</param>
        /// <returns>The predeploy's return data — the serialised request
        /// list for this block. Empty if the predeploy emitted nothing.</returns>
        public async Task<byte[]> ExecuteSystemCallAsync(
            string targetAddress,
            BlockContext blockContext,
            IStateReader stateReader = null)
        {
            if (string.IsNullOrEmpty(targetAddress)) throw new ArgumentException("targetAddress required", nameof(targetAddress));
            if (blockContext == null) throw new ArgumentNullException(nameof(blockContext));

            var snapshot = await _stateStore.CreateSnapshotAsync();

            try
            {
                IStateReader nodeDataService = stateReader ?? new StateStoreNodeDataService(_stateStore, _blockStore);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var senderAddress = Forks.Eip7685Constants.SystemAddress;
                var senderBalance = await nodeDataService.GetBalanceAsync(senderAddress);
                executionStateService.SetInitialChainBalance(senderAddress, senderBalance);

                var ctx = new TransactionExecutionContext
                {
                    Sender = senderAddress,
                    To = targetAddress,
                    Data = Array.Empty<byte>(),
                    Value = EvmUInt256.Zero,
                    GasLimit = new EvmUInt256(Forks.Eip7685Constants.SystemCallGasLimit),
                    GasPrice = EvmUInt256.Zero,
                    MaxFeePerGas = EvmUInt256.Zero,
                    MaxPriorityFeePerGas = BigInteger.Zero,
                    Nonce = EvmUInt256.Zero,
                    IsEip1559 = false,
                    IsType4Transaction = false,
                    IsContractCreation = false,
                    Mode = ExecutionMode.SystemCall,
                    BlockNumber = (long)blockContext.BlockNumber,
                    Timestamp = blockContext.Timestamp,
                    Coinbase = blockContext.Coinbase,
                    BaseFee = blockContext.BaseFee,
                    Difficulty = blockContext.Difficulty,
                    BlockGasLimit = blockContext.GasLimit,
                    ChainId = blockContext.ChainId,
                    ExecutionState = executionStateService,
                    TraceEnabled = false,
                    AccessList = null,
                    AuthorisationList = null
                };

                var evmResult = await _executor.ExecuteAsync(ctx);

                if (evmResult.IsValidationError || !evmResult.Success)
                {
                    await _stateStore.RevertSnapshotAsync(snapshot);
                    throw new InvalidOperationException(
                        $"EIP-7685 system call to {targetAddress} failed: {evmResult.Error ?? evmResult.RevertReason ?? "unknown"}");
                }

                await PersistExecutionStateChangesAsync(executionStateService);

                if (evmResult.DeletedAccounts != null)
                {
                    foreach (var deletedAddr in evmResult.DeletedAccounts)
                    {
                        if (string.IsNullOrEmpty(deletedAddr)) continue;
                        await _stateStore.ClearStorageAsync(deletedAddr);
                        await _stateStore.DeleteAccountAsync(deletedAddr);
                    }
                }

                await _stateStore.CommitSnapshotAsync(snapshot);
                return evmResult.ReturnData ?? Array.Empty<byte>();
            }
            catch
            {
                await _stateStore.RevertSnapshotAsync(snapshot);
                throw;
            }
        }

        public static BigInteger CalculateIntrinsicGas(byte[] data, bool isContractCreation)
        {
            BigInteger gas = G_TRANSACTION;

            if (isContractCreation)
            {
                gas += G_TXCREATE;

                if (data != null && data.Length > 0)
                {
                    int initcodeWords = (data.Length + 31) / 32;
                    gas += initcodeWords * 2;
                }
            }

            if (data != null && data.Length > 0)
            {
                foreach (var b in data)
                {
                    gas += b == 0 ? G_TXDATAZERO : G_TXDATANONZERO;
                }
            }

            return gas;
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

        public static TransactionData GetTransactionData(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction4844 tx4844:
                    return new TransactionData
                    {
                        Nonce = tx4844.Nonce ?? 0,
                        GasLimit = tx4844.GasLimit ?? 21000,
                        GasPrice = tx4844.MaxFeePerGas ?? 0,
                        MaxFeePerGas = tx4844.MaxFeePerGas,
                        MaxPriorityFeePerGas = tx4844.MaxPriorityFeePerGas,
                        To = tx4844.ReceiverAddress,
                        Value = tx4844.Amount ?? 0,
                        Data = tx4844.Data?.HexToByteArray(),
                        AccessList = ConvertAccessList(tx4844.AccessList),
                        BlobVersionedHashes = tx4844.BlobVersionedHashes?.Select(h => "0x" + h.ToHex()).ToList(),
                        MaxFeePerBlobGas = tx4844.MaxFeePerBlobGas
                    };

                case Transaction7702 tx7702:
                    return new TransactionData
                    {
                        Nonce = tx7702.Nonce ?? 0,
                        GasLimit = tx7702.GasLimit ?? 21000,
                        GasPrice = tx7702.MaxFeePerGas ?? 0,
                        MaxFeePerGas = tx7702.MaxFeePerGas,
                        MaxPriorityFeePerGas = tx7702.MaxPriorityFeePerGas,
                        To = tx7702.ReceiverAddress,
                        Value = tx7702.Amount ?? 0,
                        Data = tx7702.Data?.HexToByteArray(),
                        AccessList = ConvertAccessList(tx7702.AccessList),
                        AuthorisationList = tx7702.AuthorisationList
                    };

                case Transaction1559 tx1559:
                    return new TransactionData
                    {
                        Nonce = tx1559.Nonce ?? 0,
                        GasLimit = tx1559.GasLimit ?? 21000,
                        GasPrice = tx1559.MaxFeePerGas ?? 0,
                        MaxFeePerGas = tx1559.MaxFeePerGas,
                        MaxPriorityFeePerGas = tx1559.MaxPriorityFeePerGas,
                        To = tx1559.ReceiverAddress,
                        Value = tx1559.Amount ?? 0,
                        Data = tx1559.Data?.HexToByteArray(),
                        AccessList = ConvertAccessList(tx1559.AccessList)
                    };

                case Transaction2930 tx2930:
                    return new TransactionData
                    {
                        Nonce = tx2930.Nonce ?? 0,
                        GasLimit = tx2930.GasLimit ?? 21000,
                        GasPrice = tx2930.GasPrice ?? 0,
                        To = tx2930.ReceiverAddress,
                        Value = tx2930.Amount ?? 0,
                        Data = tx2930.Data?.HexToByteArray(),
                        AccessList = ConvertAccessList(tx2930.AccessList)
                    };

                case LegacyTransaction legacyTx:
                    return new TransactionData
                    {
                        Nonce = legacyTx.Nonce.ToBigIntegerFromRLPDecoded(),
                        GasLimit = legacyTx.GasLimit.ToBigIntegerFromRLPDecoded(),
                        GasPrice = legacyTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                        To = legacyTx.ReceiveAddress?.ToHex(true),
                        Value = legacyTx.Value.ToBigIntegerFromRLPDecoded(),
                        Data = legacyTx.Data
                    };

                case LegacyTransactionChainId legacyChainTx:
                    return new TransactionData
                    {
                        Nonce = legacyChainTx.Nonce.ToBigIntegerFromRLPDecoded(),
                        GasLimit = legacyChainTx.GasLimit.ToBigIntegerFromRLPDecoded(),
                        GasPrice = legacyChainTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                        To = legacyChainTx.ReceiveAddress?.ToHex(true),
                        Value = legacyChainTx.Value.ToBigIntegerFromRLPDecoded(),
                        Data = legacyChainTx.Data
                    };

                default:
                    throw new NotSupportedException($"Transaction type {tx.GetType().Name} is not supported");
            }
        }

        private static List<AccessListEntry> ConvertAccessList(List<AccessListItem> accessListItems)
        {
            if (accessListItems == null || accessListItems.Count == 0)
                return null;

            var result = new List<AccessListEntry>();
            foreach (var item in accessListItems)
            {
                var storageKeys = new List<string>();
                if (item.StorageKeys != null)
                {
                    foreach (var key in item.StorageKeys)
                    {
                        storageKeys.Add(key.ToHex(true));
                    }
                }
                result.Add(new AccessListEntry
                {
                    Address = item.Address,
                    StorageKeys = storageKeys
                });
            }
            return result;
        }

        private static bool WasSelfDestructedThisTx(List<string> deletedAccounts, string address)
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

        public static byte GetTransactionType(ISignedTransaction tx)
        {
            return tx switch
            {
                Transaction7702 => 4,
                Transaction1559 => 2,
                Transaction2930 => 1,
                _ => 0
            };
        }

    }

    public class TransactionData
    {
        public EvmUInt256 Nonce { get; set; }
        public EvmUInt256 GasLimit { get; set; }
        public EvmUInt256 GasPrice { get; set; }
        public EvmUInt256? MaxFeePerGas { get; set; }
        public EvmUInt256? MaxPriorityFeePerGas { get; set; }
        public string To { get; set; }
        public EvmUInt256 Value { get; set; }
        public byte[] Data { get; set; }
        public List<AccessListEntry> AccessList { get; set; }
        public List<Authorisation7702Signed> AuthorisationList { get; set; }

        /// <summary>
        /// EIP-4844 blob versioned hashes (one per blob). Populated only for
        /// <see cref="Transaction4844"/>; null for every other tx type. The
        /// EVM <c>BLOBHASH</c> opcode reads from this list — failing to wire
        /// it through means a blob tx's contract sees zero for every index,
        /// producing silent execution divergence vs canonical (mainnet
        /// block 20,000,000 tx[21] was the first sighting).
        /// </summary>
        public List<string> BlobVersionedHashes { get; set; }

        /// <summary>
        /// EIP-4844 max fee per blob gas. Populated only for
        /// <see cref="Transaction4844"/>; null otherwise.
        /// </summary>
        public EvmUInt256? MaxFeePerBlobGas { get; set; }

        public EvmUInt256 GetEffectiveGasPrice(EvmUInt256 baseFee)
        {
            if (MaxFeePerGas.HasValue && MaxPriorityFeePerGas.HasValue)
            {
                var diff = MaxFeePerGas.Value - baseFee;
                var priorityFee = MaxPriorityFeePerGas.Value < diff ? MaxPriorityFeePerGas.Value : diff;
                return baseFee + priorityFee;
            }
            return GasPrice;
        }
    }
}
