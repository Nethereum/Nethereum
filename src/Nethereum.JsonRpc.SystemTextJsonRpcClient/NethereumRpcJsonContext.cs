#if NET7_0_OR_GREATER
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using System.Text.Json.Serialization;

namespace Nethereum.JsonRpc.SystemTextJsonRpcClient
{
    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(RpcRequestMessage))]
    [JsonSerializable(typeof(RpcRequestMessage[]))]
    [JsonSerializable(typeof(RpcResponseMessage))]
    [JsonSerializable(typeof(RpcResponseMessage[]))]
    [JsonSerializable(typeof(Nethereum.JsonRpc.Client.RpcMessages.RpcError))]
    [JsonSerializable(typeof(HexBigInteger))]
    [JsonSerializable(typeof(HexBigInteger[]))]
    [JsonSerializable(typeof(HexUTF8String))]
    [JsonSerializable(typeof(HexUTF8String[]))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(double[][]))]
    [JsonSerializable(typeof(double[]))]
    [JsonSerializable(typeof(double))]

    [JsonSerializable(typeof(BlockParameter))]
    [JsonSerializable(typeof(BlockParameter[]))]

    [JsonSerializable(typeof(Block))]
    [JsonSerializable(typeof(Block[]))]
    [JsonSerializable(typeof(BlockWithTransactions))]
    [JsonSerializable(typeof(BlockWithTransactions[]))]
    [JsonSerializable(typeof(BlockWithTransactionHashes))]
    [JsonSerializable(typeof(BlockWithTransactionHashes[]))]

    [JsonSerializable(typeof(Transaction))]
    [JsonSerializable(typeof(Transaction[]))]
    [JsonSerializable(typeof(TransactionInput))]
    [JsonSerializable(typeof(TransactionInput[]))]
    [JsonSerializable(typeof(TransactionReceipt))]
    [JsonSerializable(typeof(TransactionReceipt[]))]

    [JsonSerializable(typeof(FeeHistoryResult))]
    [JsonSerializable(typeof(FeeHistoryResult[]))]

    [JsonSerializable(typeof(FilterLog))]
    [JsonSerializable(typeof(FilterLog[]))]
    [JsonSerializable(typeof(CallInput))]
    [JsonSerializable(typeof(CallInput[]))]

    [JsonSerializable(typeof(StateChange))]
    [JsonSerializable(typeof(StateChange[]))]
    [JsonSerializable(typeof(AccessList))]
    [JsonSerializable(typeof(AccessList[]))]
    [JsonSerializable(typeof(AccessListGasUsed))]
    [JsonSerializable(typeof(AccessListGasUsed[]))]

    [JsonSerializable(typeof(AccountProof))]
    [JsonSerializable(typeof(AccountProof[]))]
    [JsonSerializable(typeof(Authorisation))]
    [JsonSerializable(typeof(Authorisation[]))]
    [JsonSerializable(typeof(BadBlock))]
    [JsonSerializable(typeof(BadBlock[]))]
    [JsonSerializable(typeof(SyncingOutput))]
    [JsonSerializable(typeof(SyncingOutput[]))]
    [JsonSerializable(typeof(StorageProof))]
    [JsonSerializable(typeof(StorageProof[]))]

    [JsonSerializable(typeof(NewFilterInput))]
    [JsonSerializable(typeof(NewSubscriptionInput))]
    public partial class NethereumRpcJsonContext : JsonSerializerContext
    {
    }
}

#endif