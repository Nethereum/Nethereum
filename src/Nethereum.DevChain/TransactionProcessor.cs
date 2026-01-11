using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.DevChain
{
    public class TransactionProcessor
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly DevChainConfig _config;
        private readonly ITransactionVerificationAndRecovery _txVerifier;

        public const int G_TRANSACTION = 21000;
        public const int G_TXDATAZERO = 4;
        public const int G_TXDATANONZERO = 16;
        public const int G_TXCREATE = 32000;

        public TransactionProcessor(
            IStateStore stateStore,
            IBlockStore blockStore,
            DevChainConfig config,
            ITransactionVerificationAndRecovery txVerifier)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
            _config = config;
            _txVerifier = txVerifier;
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
                var isContractCreation = string.IsNullOrEmpty(txData.To);

                var intrinsicGas = CalculateIntrinsicGas(txData.Data, isContractCreation);
                if (txData.GasLimit < intrinsicGas)
                {
                    result.Success = false;
                    result.RevertReason = $"Intrinsic gas too low: have {txData.GasLimit}, want {intrinsicGas}";
                    return result;
                }

                var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
                if (senderAccount == null)
                {
                    senderAccount = new Account { Balance = 0, Nonce = 0 };
                }

                if (senderAccount.Nonce != txData.Nonce)
                {
                    result.Success = false;
                    result.RevertReason = $"Invalid nonce: have {txData.Nonce}, want {senderAccount.Nonce}";
                    return result;
                }

                var maxCost = txData.GasLimit * txData.GasPrice + txData.Value;
                if (senderAccount.Balance < maxCost)
                {
                    result.Success = false;
                    result.RevertReason = $"Insufficient funds: have {senderAccount.Balance}, want {maxCost}";
                    return result;
                }

                var snapshot = await _stateStore.CreateSnapshotAsync();

                try
                {
                    senderAccount.Nonce++;
                    await _stateStore.SaveAccountAsync(senderAddress, senderAccount);

                    var gasCost = txData.GasLimit * txData.GasPrice;
                    senderAccount.Balance -= gasCost;
                    await _stateStore.SaveAccountAsync(senderAddress, senderAccount);

                    var nodeDataService = new StateStoreNodeDataService(_stateStore, _blockStore);
                    var executionStateService = new ExecutionStateService(nodeDataService);

                    executionStateService.SetInitialChainBalance(senderAddress, senderAccount.Balance);

                    string contractAddress = null;
                    byte[] returnData = null;
                    bool executionSuccess = true;
                    string revertReason = null;
                    BigInteger gasUsed = intrinsicGas;
                    var logs = new List<Log>();

                    if (isContractCreation)
                    {
                        contractAddress = ContractUtils.CalculateContractAddress(senderAddress, txData.Nonce - 1);
                        result.ContractAddress = contractAddress;

                        var initCode = txData.Data;
                        if (initCode != null && initCode.Length > 0)
                        {
                            var callInput = CreateCallInput(
                                senderAddress,
                                contractAddress,
                                txData.Value,
                                initCode,
                                txData.GasLimit - intrinsicGas,
                                txData.GasPrice,
                                blockContext.ChainId);

                            var program = await CreateAndExecuteProgramAsync(
                                callInput,
                                executionStateService,
                                blockContext,
                                initCode);

                            gasUsed = intrinsicGas + program.TotalGasUsed;
                            executionSuccess = !program.ProgramResult.IsRevert;
                            revertReason = program.ProgramResult.GetRevertMessage();
                            returnData = program.ProgramResult.Result;

                            if (executionSuccess && returnData != null && returnData.Length > 0)
                            {
                                var codeHash = new Sha3Keccack().CalculateHash(returnData);
                                await _stateStore.SaveCodeAsync(codeHash, returnData);

                                var contractAccount = await _stateStore.GetAccountAsync(contractAddress)
                                    ?? new Account { Balance = 0, Nonce = 1 };
                                contractAccount.CodeHash = codeHash;
                                contractAccount.Nonce = 1;
                                await _stateStore.SaveAccountAsync(contractAddress, contractAccount);
                            }

                            logs = ConvertLogs(program.ProgramResult.Logs);
                        }
                        else
                        {
                            var contractAccount = new Account { Balance = txData.Value, Nonce = 1 };
                            await _stateStore.SaveAccountAsync(contractAddress, contractAccount);
                        }
                    }
                    else
                    {
                        var receiverAccount = await _stateStore.GetAccountAsync(txData.To);
                        var hasCode = receiverAccount?.CodeHash != null;

                        if (hasCode)
                        {
                            var code = await _stateStore.GetCodeAsync(receiverAccount.CodeHash);
                            if (code != null && code.Length > 0)
                            {
                                var callInput = CreateCallInput(
                                    senderAddress,
                                    txData.To,
                                    txData.Value,
                                    txData.Data,
                                    txData.GasLimit - intrinsicGas,
                                    txData.GasPrice,
                                    blockContext.ChainId);

                                var program = await CreateAndExecuteProgramAsync(
                                    callInput,
                                    executionStateService,
                                    blockContext,
                                    code);

                                gasUsed = intrinsicGas + program.TotalGasUsed;
                                executionSuccess = !program.ProgramResult.IsRevert;
                                revertReason = program.ProgramResult.GetRevertMessage();
                                returnData = program.ProgramResult.Result;
                                logs = ConvertLogs(program.ProgramResult.Logs);
                            }
                        }

                        if (executionSuccess)
                        {
                            await TransferValueAsync(senderAddress, txData.To, txData.Value);
                        }
                    }

                    if (gasUsed > txData.GasLimit)
                    {
                        gasUsed = txData.GasLimit;
                    }

                    var gasRefund = txData.GasLimit - gasUsed;
                    if (gasRefund > 0)
                    {
                        var refundAmount = gasRefund * txData.GasPrice;
                        var updatedSender = await _stateStore.GetAccountAsync(senderAddress);
                        updatedSender.Balance += refundAmount;
                        await _stateStore.SaveAccountAsync(senderAddress, updatedSender);
                    }

                    var coinbaseAccount = await _stateStore.GetAccountAsync(blockContext.Coinbase)
                        ?? new Account { Balance = 0, Nonce = 0 };
                    coinbaseAccount.Balance += gasUsed * txData.GasPrice;
                    await _stateStore.SaveAccountAsync(blockContext.Coinbase, coinbaseAccount);

                    if (!executionSuccess)
                    {
                        await _stateStore.RevertSnapshotAsync(snapshot);

                        senderAccount = await _stateStore.GetAccountAsync(senderAddress);
                        senderAccount.Nonce++;
                        senderAccount.Balance -= gasUsed * txData.GasPrice;
                        await _stateStore.SaveAccountAsync(senderAddress, senderAccount);

                        coinbaseAccount = await _stateStore.GetAccountAsync(blockContext.Coinbase)
                            ?? new Account { Balance = 0, Nonce = 0 };
                        coinbaseAccount.Balance += gasUsed * txData.GasPrice;
                        await _stateStore.SaveAccountAsync(blockContext.Coinbase, coinbaseAccount);

                        logs.Clear();
                    }
                    else
                    {
                        await PersistExecutionStateChangesAsync(executionStateService);
                        await _stateStore.CommitSnapshotAsync(snapshot);
                    }

                    result.Success = executionSuccess;
                    result.GasUsed = gasUsed;
                    result.CumulativeGasUsed = cumulativeGasUsed + gasUsed;
                    result.ReturnData = returnData;
                    result.RevertReason = revertReason;
                    result.Logs = logs;

                    var bloom = CalculateLogsBloom(logs);
                    result.Receipt = Receipt.CreateStatusReceipt(
                        executionSuccess,
                        result.CumulativeGasUsed,
                        bloom,
                        logs);
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

                if (accountState.Code != null && accountState.Code.Length > 0)
                {
                    var existingAccount = await _stateStore.GetAccountAsync(address);
                    if (existingAccount == null || existingAccount.CodeHash == null)
                    {
                        var codeHash = new Sha3Keccack().CalculateHash(accountState.Code);
                        await _stateStore.SaveCodeAsync(codeHash, accountState.Code);

                        var account = existingAccount ?? new Account { Balance = 0, Nonce = 1 };
                        account.CodeHash = codeHash;
                        await _stateStore.SaveAccountAsync(address, account);
                    }
                }
            }
        }

        private async Task<Program> CreateAndExecuteProgramAsync(
            CallInput callInput,
            ExecutionStateService executionStateService,
            BlockContext blockContext,
            byte[] code)
        {
            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                callInput.From,
                callInput.To,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator();
            await simulator.ExecuteAsync(program, traceEnabled: false);

            return program;
        }

        private CallInput CreateCallInput(
            string from,
            string to,
            BigInteger value,
            byte[] data,
            BigInteger gas,
            BigInteger gasPrice,
            BigInteger chainId)
        {
            return new CallInput
            {
                From = from,
                To = to,
                Value = new Hex.HexTypes.HexBigInteger(value),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new Hex.HexTypes.HexBigInteger(gas),
                GasPrice = new Hex.HexTypes.HexBigInteger(gasPrice),
                ChainId = new Hex.HexTypes.HexBigInteger(chainId)
            };
        }

        private async Task TransferValueAsync(string from, string to, BigInteger value)
        {
            if (value <= 0) return;

            var fromAccount = await _stateStore.GetAccountAsync(from);
            var toAccount = await _stateStore.GetAccountAsync(to) ?? new Account { Balance = 0, Nonce = 0 };

            fromAccount.Balance -= value;
            toAccount.Balance += value;

            await _stateStore.SaveAccountAsync(from, fromAccount);
            await _stateStore.SaveAccountAsync(to, toAccount);
        }

        public static BigInteger CalculateIntrinsicGas(byte[] data, bool isContractCreation)
        {
            BigInteger gas = G_TRANSACTION;

            if (isContractCreation)
            {
                gas += G_TXCREATE;
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

        private TransactionData GetTransactionData(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction1559 tx1559:
                    return new TransactionData
                    {
                        Nonce = tx1559.Nonce ?? 0,
                        GasLimit = tx1559.GasLimit ?? 21000,
                        GasPrice = tx1559.MaxFeePerGas ?? 0,
                        To = tx1559.ReceiverAddress,
                        Value = tx1559.Amount ?? 0,
                        Data = tx1559.Data?.HexToByteArray()
                    };

                case Transaction2930 tx2930:
                    return new TransactionData
                    {
                        Nonce = tx2930.Nonce ?? 0,
                        GasLimit = tx2930.GasLimit ?? 21000,
                        GasPrice = tx2930.GasPrice ?? 0,
                        To = tx2930.ReceiverAddress,
                        Value = tx2930.Amount ?? 0,
                        Data = tx2930.Data?.HexToByteArray()
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

        private class TransactionData
        {
            public BigInteger Nonce { get; set; }
            public BigInteger GasLimit { get; set; }
            public BigInteger GasPrice { get; set; }
            public string To { get; set; }
            public BigInteger Value { get; set; }
            public byte[] Data { get; set; }
        }
    }
}
