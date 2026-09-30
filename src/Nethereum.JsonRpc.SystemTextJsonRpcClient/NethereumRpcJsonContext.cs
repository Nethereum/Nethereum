#if NET7_0_OR_GREATER
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.RPC.TxPool.DTOs;
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
    [JsonSerializable(typeof(ChainConfiguration))]
    [JsonSerializable(typeof(ChainConfigurationEntry))]
    [JsonSerializable(typeof(BlobScheduleConfiguration))]
    [JsonSerializable(typeof(EthSimulateInput))]
    [JsonSerializable(typeof(BlockStateCall))]
    [JsonSerializable(typeof(BlockOverrides))]
    [JsonSerializable(typeof(AccountOverride))]
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, AccountOverride>))]
    [JsonSerializable(typeof(EthSimulateCallResult))]
    [JsonSerializable(typeof(EthSimulateBlockResult))]
    [JsonSerializable(typeof(EthSimulateBlockResult[]))]
    [JsonSerializable(typeof(System.Collections.Generic.List<EthSimulateBlockResult>))]
    [JsonSerializable(typeof(AccountAccess))]
    [JsonSerializable(typeof(AccountAccess[]))]
    [JsonSerializable(typeof(System.Collections.Generic.List<AccountAccess>))]
    [JsonSerializable(typeof(SlotChanges))]
    [JsonSerializable(typeof(StorageChange))]
    [JsonSerializable(typeof(BalanceChange))]
    [JsonSerializable(typeof(NonceChange))]
    [JsonSerializable(typeof(CodeChange))]
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

    [JsonSerializable(typeof(EthCapabilitiesResult))]
    [JsonSerializable(typeof(EthCapabilitiesHead))]
    [JsonSerializable(typeof(EthCapabilitiesEffectiveResource))]
    [JsonSerializable(typeof(EthCapabilitiesDeleteStrategy))]
    [JsonSerializable(typeof(PendingTransactionInfo))]
    [JsonSerializable(typeof(TxPoolContentResponse))]
    [JsonSerializable(typeof(TxPoolContentFromResponse))]
    [JsonSerializable(typeof(TxPoolStatusResponse))]
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, PendingTransactionInfo>))]
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, PendingTransactionInfo>>))]

    [JsonSerializable(typeof(ExecutionPayloadV1))]
    [JsonSerializable(typeof(ExecutionPayloadV2))]
    [JsonSerializable(typeof(ExecutionPayloadV3))]
    [JsonSerializable(typeof(ExecutionPayloadV4))]
    [JsonSerializable(typeof(ForkchoiceStateV1))]
    [JsonSerializable(typeof(PayloadAttributesV1))]
    [JsonSerializable(typeof(PayloadAttributesV2))]
    [JsonSerializable(typeof(PayloadAttributesV3))]
    [JsonSerializable(typeof(PayloadAttributesV4))]
    [JsonSerializable(typeof(BlobsBundleV1))]
    [JsonSerializable(typeof(GetPayloadV4Response))]
    [JsonSerializable(typeof(GetPayloadV6Response))]
    [JsonSerializable(typeof(PayloadStatusV1))]
    [JsonSerializable(typeof(ForkchoiceUpdatedResponseV1))]
    public partial class NethereumRpcJsonContext : JsonSerializerContext
    {
    }
}

#endif