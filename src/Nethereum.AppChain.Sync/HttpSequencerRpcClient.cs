using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class HttpSequencerRpcClient : ISequencerRpcClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _rpcUrl;
        private int _requestId;

        public HttpSequencerRpcClient(string rpcUrl, HttpClient? httpClient = null)
        {
            _rpcUrl = rpcUrl ?? throw new ArgumentNullException(nameof(rpcUrl));
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("eth_blockNumber", Array.Empty<object>(), cancellationToken);
            var result = response.GetProperty("result").GetString();
            if (string.IsNullOrEmpty(result))
                return 0;
            return result.HexToBigInteger(false);
        }

        public async Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var header = await GetBlockHeaderAsync(blockNumber, cancellationToken);
            if (header == null)
                return null;

            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(header);
            return new Nethereum.Util.Sha3Keccack().CalculateHash(encoded);
        }

        public async Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var blockHex = $"0x{blockNumber:x}";
            var response = await SendRequestAsync("eth_getBlockByNumber", new object[] { blockHex, false }, cancellationToken);

            if (response.TryGetProperty("result", out var result) && result.ValueKind != JsonValueKind.Null)
            {
                return ParseBlockHeader(result);
            }
            return null;
        }

        public async Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var blockHex = $"0x{blockNumber:x}";

            var blockResponse = await SendRequestAsync("eth_getBlockByNumber", new object[] { blockHex, true }, cancellationToken);
            if (!blockResponse.TryGetProperty("result", out var blockResult) || blockResult.ValueKind == JsonValueKind.Null)
                return null;

            var header = ParseBlockHeader(blockResult);
            var transactions = ParseTransactions(blockResult);
            var blockHash = ParseBlockHash(blockResult);

            var receiptsResponse = await SendRequestAsync("eth_getBlockReceipts", new object[] { blockHex }, cancellationToken);
            var receipts = new List<Receipt>();
            if (receiptsResponse.TryGetProperty("result", out var receiptsResult) && receiptsResult.ValueKind == JsonValueKind.Array)
            {
                foreach (var receiptJson in receiptsResult.EnumerateArray())
                {
                    var receipt = ParseReceipt(receiptJson);
                    if (receipt != null)
                    {
                        receipts.Add(receipt);
                    }
                }
            }

            return new LiveBlockData
            {
                Header = header,
                Transactions = transactions,
                Receipts = receipts,
                BlockHash = blockHash,
                IsSoft = true
            };
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

        private BlockHeader ParseBlockHeader(JsonElement json)
        {
            return new BlockHeader
            {
                ParentHash = GetHexBytes(json, "parentHash", 32),
                UnclesHash = GetHexBytes(json, "sha3Uncles", 32),
                Coinbase = GetString(json, "miner") ?? "",
                StateRoot = GetHexBytes(json, "stateRoot", 32),
                TransactionsHash = GetHexBytes(json, "transactionsRoot", 32),
                ReceiptHash = GetHexBytes(json, "receiptsRoot", 32),
                LogsBloom = GetHexBytes(json, "logsBloom", 256),
                Difficulty = GetBigInteger(json, "difficulty"),
                BlockNumber = GetLong(json, "number"),
                GasLimit = GetLong(json, "gasLimit"),
                GasUsed = GetLong(json, "gasUsed"),
                Timestamp = GetLong(json, "timestamp"),
                ExtraData = GetHexBytes(json, "extraData", 0),
                MixHash = GetHexBytes(json, "mixHash", 32),
                Nonce = GetHexBytes(json, "nonce", 8),
                BaseFee = GetBigInteger(json, "baseFeePerGas")
            };
        }

        private byte[] ParseBlockHash(JsonElement json)
        {
            return GetHexBytes(json, "hash", 32);
        }

        private List<ISignedTransaction> ParseTransactions(JsonElement json)
        {
            var transactions = new List<ISignedTransaction>();

            if (json.TryGetProperty("transactions", out var txsArray) && txsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var txJson in txsArray.EnumerateArray())
                {
                    if (txJson.ValueKind == JsonValueKind.String)
                    {
                        continue;
                    }

                    var tx = ParseTransaction(txJson);
                    if (tx != null)
                    {
                        transactions.Add(tx);
                    }
                }
            }

            return transactions;
        }

        private ISignedTransaction? ParseTransaction(JsonElement json)
        {
            var type = (byte?)GetInt(json, "type");
            var chainId = GetBigInteger(json, "chainId");
            var nonce = GetBigInteger(json, "nonce");
            var to = GetString(json, "to");
            var value = GetBigInteger(json, "value");
            var data = GetString(json, "input") ?? "0x";
            var gasLimit = GetBigInteger(json, "gas");
            var gasPrice = GetBigInteger(json, "gasPrice");
            var maxFeePerGas = GetBigInteger(json, "maxFeePerGas");
            var maxPriorityFeePerGas = GetBigInteger(json, "maxPriorityFeePerGas");

            var r = GetString(json, "r") ?? "0x0";
            var s = GetString(json, "s") ?? "0x0";
            var v = GetString(json, "v") ?? "0x0";

            var accessList = ParseAccessList(json);
            var authorizationList = ParseAuthorizationList(json);

            return TransactionFactory.CreateTransaction(
                chainId,
                type,
                nonce,
                maxPriorityFeePerGas,
                maxFeePerGas,
                gasPrice,
                gasLimit,
                to,
                value,
                data,
                accessList,
                authorizationList,
                r,
                s,
                v);
        }

        private List<AccessListItem>? ParseAccessList(JsonElement json)
        {
            if (!json.TryGetProperty("accessList", out var accessListArray) || accessListArray.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<AccessListItem>();
            foreach (var item in accessListArray.EnumerateArray())
            {
                var address = GetString(item, "address");
                if (string.IsNullOrEmpty(address))
                    continue;

                var storageKeys = new List<byte[]>();
                if (item.TryGetProperty("storageKeys", out var keysArray) && keysArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var keyJson in keysArray.EnumerateArray())
                    {
                        var keyStr = keyJson.GetString();
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            storageKeys.Add(keyStr.HexToByteArray());
                        }
                    }
                }

                result.Add(new AccessListItem { Address = address, StorageKeys = storageKeys });
            }

            return result.Count > 0 ? result : null;
        }

        private List<Authorisation7702Signed>? ParseAuthorizationList(JsonElement json)
        {
            if (!json.TryGetProperty("authorizationList", out var authListArray) || authListArray.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<Authorisation7702Signed>();
            foreach (var item in authListArray.EnumerateArray())
            {
                var chainId = GetBigInteger(item, "chainId");
                var address = GetString(item, "address");
                var nonceVal = GetBigInteger(item, "nonce");

                var yParity = GetHexBytes(item, "yParity", 0);
                if (yParity.Length == 0)
                {
                    yParity = GetHexBytes(item, "v", 0);
                }
                var r = GetHexBytes(item, "r", 0);
                var s = GetHexBytes(item, "s", 0);

                if (string.IsNullOrEmpty(address))
                    continue;

                result.Add(new Authorisation7702Signed
                {
                    ChainId = chainId,
                    Address = address,
                    Nonce = nonceVal,
                    V = yParity,
                    R = r,
                    S = s
                });
            }

            return result.Count > 0 ? result : null;
        }

        private Receipt? ParseReceipt(JsonElement json)
        {
            var status = GetBigInteger(json, "status") == 1;
            var cumulativeGasUsed = GetBigInteger(json, "cumulativeGasUsed");
            var logsBloom = GetHexBytes(json, "logsBloom", 256);
            var logs = new List<Log>();

            if (json.TryGetProperty("logs", out var logsArray) && logsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var logJson in logsArray.EnumerateArray())
                {
                    var log = ParseLog(logJson);
                    if (log != null)
                    {
                        logs.Add(log);
                    }
                }
            }

            return Receipt.CreateStatusReceipt(status, cumulativeGasUsed, logsBloom, logs);
        }

        private Log ParseLog(JsonElement json)
        {
            var address = GetString(json, "address") ?? "";
            var topics = new List<byte[]>();

            if (json.TryGetProperty("topics", out var topicsArray) && topicsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var topicJson in topicsArray.EnumerateArray())
                {
                    var topic = topicJson.GetString()?.HexToByteArray() ?? Array.Empty<byte>();
                    topics.Add(topic);
                }
            }

            var data = GetHexBytes(json, "data", 0);

            return Log.Create(data, address, topics.ToArray());
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

        private long GetLong(JsonElement json, string property)
        {
            return (long)GetBigInteger(json, property);
        }

        private int GetInt(JsonElement json, string property)
        {
            return (int)GetBigInteger(json, property);
        }
    }
}
