using System;
using System.Collections.Generic;
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
            HardforkConfig hardforkConfig = null)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
            _config = config;
            _txVerifier = txVerifier;
            _hardforkConfig = hardforkConfig ?? HardforkConfig.Default;
            _executor = new TransactionExecutor(_hardforkConfig);
        }

        public async Task<TransactionExecutionResult> ExecuteTransactionAsync(
            ISignedTransaction signedTx,
            BlockContext blockContext,
            int txIndex,
            BigInteger cumulativeGasUsed)
        {
            var result = new TransactionExecutionResult
            {
                Transaction = signedTx,
                TransactionHash = signedTx.Hash,
                TransactionIndex = txIndex
            };

            try
            {
                var senderAddress = _txVerifier.GetSenderAddress(signedTx);
                if (string.IsNullOrEmpty(senderAddress))
                {
                    result.Success = false;
                    result.RevertReason = "Invalid signature: cannot recover sender address";
                    return result;
                }

                var txData = GetTransactionData(signedTx);

                var snapshot = await _stateStore.CreateSnapshotAsync();

                try
                {
                    var nodeDataService = new StateStoreNodeDataService(_stateStore, _blockStore);
                    var executionStateService = new ExecutionStateService(nodeDataService);

                    var senderBalance = await nodeDataService.GetBalanceAsync(senderAddress);
                    executionStateService.SetInitialChainBalance(senderAddress, senderBalance);

                    var ctx = BuildExecutionContext(txData, senderAddress, blockContext, executionStateService);

                    var evmResult = await _executor.ExecuteAsync(ctx);

                    if (evmResult.IsValidationError)
                    {
                        await _stateStore.RevertSnapshotAsync(snapshot);
                        result.Success = false;
                        result.RevertReason = evmResult.Error;
                        result.GasUsed = 0;
                        result.CumulativeGasUsed = cumulativeGasUsed;
                        result.Receipt = Receipt.CreateStatusReceipt(false, result.CumulativeGasUsed, new byte[256], new List<Log>());
                        return result;
                    }

                    await PersistExecutionStateChangesAsync(executionStateService);
                    await _stateStore.CommitSnapshotAsync(snapshot);

                    result.Success = evmResult.Success;
                    result.GasUsed = evmResult.GasUsed;
                    result.CumulativeGasUsed = cumulativeGasUsed + evmResult.GasUsed;
                    result.ReturnData = evmResult.ReturnData;
                    result.RevertReason = evmResult.RevertReason ?? evmResult.Error;
                    result.ContractAddress = evmResult.ContractAddress;

                    if (evmResult.Success)
                    {
                        result.Logs = ConvertLogs(evmResult.Logs);
                    }
                    else
                    {
                        result.Logs = new List<Log>();
                    }

                    var bloom = CalculateLogsBloom(result.Logs);
                    result.Receipt = Receipt.CreateStatusReceipt(
                        evmResult.Success,
                        result.CumulativeGasUsed,
                        bloom,
                        result.Logs);
                }
                catch (Exception ex)
                {
                    await _stateStore.RevertSnapshotAsync(snapshot);
                    result.Success = false;
                    result.RevertReason = ex.Message;
                    result.GasUsed = txData.GasLimit;
                    result.CumulativeGasUsed = cumulativeGasUsed + txData.GasLimit;
                    result.Receipt = Receipt.CreateStatusReceipt(false, result.CumulativeGasUsed, new byte[256], new List<Log>());
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.RevertReason = ex.Message;
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
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ExecutionState = executionState,
                TraceEnabled = false,
                AccessList = txData.AccessList
            };
        }

        private async Task PersistExecutionStateChangesAsync(ExecutionStateService executionStateService)
        {
            foreach (var accountKvp in executionStateService.AccountsState)
            {
                var address = accountKvp.Key;
                var accountState = accountKvp.Value;

                foreach (var storageKvp in accountState.Storage)
                {
                    var slot = storageKvp.Key;
                    var value = storageKvp.Value;
                    await _stateStore.SaveStorageAsync(address, slot, value);
                }

                var existingAccount = await _stateStore.GetAccountAsync(address);
                var account = existingAccount ?? new Account { Balance = 0, Nonce = 0 };
                var needsSave = false;

                if (accountState.Code != null && accountState.Code.Length > 0 && IsEmptyCodeHash(account.CodeHash))
                {
                    var codeHash = new Sha3Keccack().CalculateHash(accountState.Code);
                    await _stateStore.SaveCodeAsync(codeHash, accountState.Code);
                    account.CodeHash = codeHash;
                    needsSave = true;
                }

                var newBalance = accountState.Balance.GetTotalBalance();
                if (account.Balance != newBalance)
                {
                    account.Balance = newBalance;
                    needsSave = true;
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
            var hash = new Sha3Keccack().CalculateHash(data);

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

    public class TransactionData
    {
        public BigInteger Nonce { get; set; }
        public BigInteger GasLimit { get; set; }
        public BigInteger GasPrice { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public string To { get; set; }
        public BigInteger Value { get; set; }
        public byte[] Data { get; set; }
        public List<AccessListEntry> AccessList { get; set; }

        public BigInteger GetEffectiveGasPrice(BigInteger baseFee)
        {
            if (MaxFeePerGas.HasValue && MaxPriorityFeePerGas.HasValue)
            {
                var priorityFee = BigInteger.Min(MaxPriorityFeePerGas.Value, MaxFeePerGas.Value - baseFee);
                return baseFee + priorityFee;
            }
            return GasPrice;
        }
    }
}
