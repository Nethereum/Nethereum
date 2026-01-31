using System.Collections.Generic;
using System.Text.Json.Serialization;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(JsonRpcRequest))]
    [JsonSerializable(typeof(JsonRpcResponse))]
    [JsonSerializable(typeof(JsonRpcError))]
    [JsonSerializable(typeof(CallInput))]
    [JsonSerializable(typeof(NewFilterInput))]
    [JsonSerializable(typeof(HexBigInteger))]
    [JsonSerializable(typeof(HexUTF8String))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<double>))]
    [JsonSerializable(typeof(List<object>))]
    [JsonSerializable(typeof(TransactionInput))]
    [JsonSerializable(typeof(Transaction))]
    [JsonSerializable(typeof(TransactionReceipt))]
    [JsonSerializable(typeof(Block))]
    [JsonSerializable(typeof(BlockWithTransactions))]
    [JsonSerializable(typeof(BlockWithTransactionHashes))]
    [JsonSerializable(typeof(FilterLog))]
    [JsonSerializable(typeof(FilterLog[]))]
    [JsonSerializable(typeof(List<FilterLog>))]
    [JsonSerializable(typeof(byte[]))]
    [JsonSerializable(typeof(AccessList))]
    [JsonSerializable(typeof(AccessListItem))]
    [JsonSerializable(typeof(FeeHistoryResult))]
    [JsonSerializable(typeof(AccountProof))]
    [JsonSerializable(typeof(StorageProof))]
    public partial class CoreChainJsonContext : JsonSerializerContext
    {
    }
}
