using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class HttpSequencerTxProxy : ISequencerTxProxy
    {
        private readonly HttpClient _httpClient;
        private readonly string _rpcUrl;
        private readonly IReceiptStore? _localReceiptStore;
        private int _requestId;

        public HttpSequencerTxProxy(string sequencerRpcUrl, IReceiptStore? localReceiptStore = null, HttpClient? httpClient = null)
        {
            _rpcUrl = sequencerRpcUrl ?? throw new ArgumentNullException(nameof(sequencerRpcUrl));
            _localReceiptStore = localReceiptStore;
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<byte[]> SendRawTransactionAsync(byte[] rawTransaction, CancellationToken cancellationToken = default)
        {
            var rawTxHex = rawTransaction.ToHex(true);
            var response = await SendRequestAsync("eth_sendRawTransaction", new object[] { rawTxHex }, cancellationToken);

            if (response.TryGetProperty("error", out var error))
            {
                var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                throw new InvalidOperationException($"Transaction submission failed: {errorMessage}");
            }

            if (response.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String)
            {
                var txHashHex = result.GetString();
                if (!string.IsNullOrEmpty(txHashHex))
                {
                    return txHashHex.HexToByteArray();
                }
            }

            throw new InvalidOperationException("Invalid response from eth_sendRawTransaction");
        }

        public async Task<ReceiptInfo?> WaitForReceiptAsync(
            byte[] txHash,
            int timeoutMs = 30000,
            int pollIntervalMs = 500,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var backoff = pollIntervalMs;
            var maxBackoff = 5000;

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_localReceiptStore != null)
                {
                    var localReceipt = await _localReceiptStore.GetInfoByTxHashAsync(txHash);
                    if (localReceipt != null)
                    {
                        return localReceipt;
                    }
                }

                var remoteReceipt = await GetTransactionReceiptAsync(txHash, cancellationToken);
                if (remoteReceipt != null)
                {
                    return remoteReceipt;
                }

                await Task.Delay(backoff, cancellationToken);
                backoff = Math.Min(backoff + pollIntervalMs / 2, maxBackoff);
            }

            return null;
        }

        public async Task<ReceiptInfo?> GetTransactionReceiptAsync(byte[] txHash, CancellationToken cancellationToken = default)
        {
            var txHashHex = txHash.ToHex(true);
            var response = await SendRequestAsync("eth_getTransactionReceipt", new object[] { txHashHex }, cancellationToken);

            if (response.TryGetProperty("result", out var result) && result.ValueKind != JsonValueKind.Null)
            {
                return ParseReceiptInfo(result);
            }

            return null;
        }

        private async Task<JsonElement> SendRequestAsync(string method, object[] parameters, CancellationToken cancellationToken)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new
            {
                jsonrpc = "2.0",
                method,
                @params = parameters,
                id = requestId
            };

            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_rpcUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseJson);
            return document.RootElement.Clone();
        }

        private ReceiptInfo ParseReceiptInfo(JsonElement json)
        {
            var status = GetBigInteger(json, "status") == 1;
            var cumulativeGasUsed = GetBigInteger(json, "cumulativeGasUsed");
            var logsBloom = GetHexBytes(json, "logsBloom", 256);
            var logs = ParseLogs(json);

            var receipt = Receipt.CreateStatusReceipt(status, cumulativeGasUsed, logsBloom, logs);

            return new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = GetHexBytes(json, "transactionHash", 32),
                BlockHash = GetHexBytes(json, "blockHash", 32),
                BlockNumber = GetBigInteger(json, "blockNumber"),
                TransactionIndex = (int)GetBigInteger(json, "transactionIndex"),
                GasUsed = GetBigInteger(json, "gasUsed"),
                ContractAddress = GetString(json, "contractAddress"),
                EffectiveGasPrice = GetBigInteger(json, "effectiveGasPrice")
            };
        }

        private List<Log> ParseLogs(JsonElement json)
        {
            var logs = new List<Log>();

            if (json.TryGetProperty("logs", out var logsArray) && logsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var logJson in logsArray.EnumerateArray())
                {
                    var address = GetString(logJson, "address") ?? "";
                    var topics = new List<byte[]>();

                    if (logJson.TryGetProperty("topics", out var topicsArray) && topicsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var topicJson in topicsArray.EnumerateArray())
                        {
                            var topic = topicJson.GetString()?.HexToByteArray() ?? Array.Empty<byte>();
                            topics.Add(topic);
                        }
                    }

                    var data = GetHexBytes(logJson, "data", 0);
                    logs.Add(Log.Create(data, address, topics.ToArray()));
                }
            }

            return logs;
        }

        private byte[] GetHexBytes(JsonElement json, string property, int expectedLength)
        {
            if (json.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var hexStr = prop.GetString();
                if (!string.IsNullOrEmpty(hexStr))
                {
                    var bytes = hexStr.HexToByteArray();
                    if (expectedLength > 0 && bytes.Length < expectedLength)
                    {
                        return bytes.PadBytes(expectedLength);
                    }
                    return bytes;
                }
            }
            return expectedLength > 0 ? new byte[expectedLength] : Array.Empty<byte>();
        }

        private string? GetString(JsonElement json, string property)
        {
            if (json.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }

        private BigInteger GetBigInteger(JsonElement json, string property)
        {
            if (json.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var hexStr = prop.GetString();
                if (!string.IsNullOrEmpty(hexStr))
                {
                    return hexStr.HexToBigInteger(false);
                }
            }
            return BigInteger.Zero;
        }
    }
}
