using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    public class CapturedTransactionInput
    {
        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("gas")]
        public string Gas { get; set; }

        [JsonProperty("chainId")]
        public long ChainId { get; set; }

        public static CapturedTransactionInput FromTransactionInput(TransactionInput txnInput)
        {
            return new CapturedTransactionInput
            {
                From = txnInput.From,
                To = txnInput.To,
                Data = txnInput.Data,
                Value = txnInput.Value?.Value.ToString(),
                Gas = txnInput.Gas?.Value.ToString(),
                ChainId = (long)(txnInput.ChainId?.Value ?? 1)
            };
        }

        public TransactionInput ToTransactionInput()
        {
            return new TransactionInput
            {
                From = From,
                To = To,
                Data = Data,
                Value = string.IsNullOrEmpty(Value) ? null : new HexBigInteger(BigInteger.Parse(Value)),
                Gas = string.IsNullOrEmpty(Gas) ? null : new HexBigInteger(BigInteger.Parse(Gas)),
                ChainId = new HexBigInteger(ChainId)
            };
        }
    }

    public class CapturedExecutionState
    {
        [JsonProperty("transactionHash")]
        public string TransactionHash { get; set; }

        [JsonProperty("blockNumber")]
        public long BlockNumber { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("transactionInput")]
        public CapturedTransactionInput TransactionInput { get; set; }

        [JsonProperty("contractCode")]
        public string ContractCode { get; set; }

        [JsonProperty("accounts")]
        public Dictionary<string, CapturedAccountState> Accounts { get; set; } = new Dictionary<string, CapturedAccountState>();

        [JsonProperty("expectedLogCount")]
        public int ExpectedLogCount { get; set; }

        [JsonProperty("expectedTraceCount")]
        public int ExpectedTraceCount { get; set; }

        [JsonProperty("expectedIsRevert")]
        public bool ExpectedIsRevert { get; set; }

        public static CapturedExecutionState CaptureFromExecution(
            ExecutionStateService executionStateService,
            string transactionHash,
            long blockNumber,
            long timestamp,
            TransactionInput txnInput,
            string contractCode,
            int logCount,
            int traceCount,
            bool isRevert)
        {
            var captured = new CapturedExecutionState
            {
                TransactionHash = transactionHash,
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                TransactionInput = CapturedTransactionInput.FromTransactionInput(txnInput),
                ContractCode = contractCode,
                ExpectedLogCount = logCount,
                ExpectedTraceCount = traceCount,
                ExpectedIsRevert = isRevert
            };

            foreach (var kvp in executionStateService.AccountsState)
            {
                var accountState = kvp.Value;
                var capturedAccount = new CapturedAccountState
                {
                    Address = accountState.Address,
                    Nonce = accountState.Nonce,
                    Code = accountState.Code?.ToHex(),
                    Balance = accountState.Balance?.InitialChainBalance?.ToString()
                };

                foreach (var storageKvp in accountState.OriginalStorageValues)
                {
                    if (storageKvp.Value != null)
                    {
                        capturedAccount.Storage[storageKvp.Key.ToString()] = storageKvp.Value.ToHex();
                    }
                }

                captured.Accounts[kvp.Key] = capturedAccount;
            }

            return captured;
        }

        public void ConfigureExecutionState(ExecutionStateService executionStateService)
        {
            foreach (var kvp in Accounts)
            {
                var capturedAccount = kvp.Value;
                var accountState = executionStateService.CreateOrGetAccountExecutionState(capturedAccount.Address);

                if (!string.IsNullOrEmpty(capturedAccount.Code))
                {
                    accountState.Code = capturedAccount.Code.HexToByteArray();
                }

                if (capturedAccount.Nonce.HasValue)
                {
                    accountState.Nonce = capturedAccount.Nonce.Value;
                }

                if (!string.IsNullOrEmpty(capturedAccount.Balance))
                {
                    accountState.Balance.SetInitialChainBalance(BigInteger.Parse(capturedAccount.Balance));
                }

                foreach (var storageKvp in capturedAccount.Storage)
                {
                    var key = BigInteger.Parse(storageKvp.Key);
                    var value = storageKvp.Value.HexToByteArray();
                    accountState.UpsertStorageValue(key, value);
                }
            }
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static CapturedExecutionState FromJson(string json)
        {
            return JsonConvert.DeserializeObject<CapturedExecutionState>(json);
        }

        public void SaveToFile(string filePath)
        {
            File.WriteAllText(filePath, ToJson());
        }

        public static CapturedExecutionState LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return FromJson(json);
        }
    }

    public class CapturedAccountState
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("nonce")]
        public BigInteger? Nonce { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("balance")]
        public string Balance { get; set; }

        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; } = new Dictionary<string, string>();
    }
}
