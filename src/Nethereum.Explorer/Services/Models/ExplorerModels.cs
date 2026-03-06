using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.DebugNode.Tracers;

namespace Nethereum.Explorer.Services;

public static class ExplorerConstants
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static int ClampPageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}

public enum AbiSource
{
    LocalUpload,
    Sourcify,
    Directory,
    FourByte
}

public class DecodedFunctionCall
{
    public string FunctionName { get; set; } = "";
    public string Signature { get; set; } = "";
    public List<ParameterOutput> Parameters { get; set; } = new();
}

public class DecodedEventLog
{
    public string EventName { get; set; } = "";
    public string Signature { get; set; } = "";
    public string LogIndex { get; set; } = "";
    public string Address { get; set; } = "";
    public List<ParameterOutput> Parameters { get; set; } = new();
    public bool IsDecoded { get; set; }
}

public class DecodedError
{
    public string ErrorName { get; set; } = "";
    public string Signature { get; set; } = "";
    public List<ParameterOutput> Parameters { get; set; } = new();
    public bool IsStandardRevert { get; set; }
}

public class RawEventLog
{
    public string LogIndex { get; set; } = "";
    public string Address { get; set; } = "";
    public string? EventHash { get; set; }
    public string? IndexVal1 { get; set; }
    public string? IndexVal2 { get; set; }
    public string? IndexVal3 { get; set; }
    public string? Data { get; set; }
}

public class NftTransferRecord
{
    public string ContractAddress { get; set; } = "";
    public string TokenId { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public class BlobTransactionInfo
{
    public string MaxFeePerBlobGas { get; set; } = "";
    public List<string> BlobVersionedHashes { get; set; } = new();
    public int BlobCount => BlobVersionedHashes.Count;
}

public class NftTokenInfo
{
    public string ContractAddress { get; set; } = "";
    public string TokenId { get; set; } = "";
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public string? TokenUri { get; set; }
}

public class AuthorizationInfo
{
    public string ChainId { get; set; } = "";
    public string Address { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string YParity { get; set; } = "";
    public string R { get; set; } = "";
    public string S { get; set; } = "";
}

public class TokenInfo
{
    public string Address { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public int Decimals { get; set; } = 18;
    public BigInteger Balance { get; set; }
}

public class TransactionTraceResult
{
    public CallTracerResponse? CallTrace { get; set; }
    public List<FlattenedInternalCall> InternalCalls { get; set; } = new();
    public string? Error { get; set; }
}

public class FlattenedInternalCall
{
    public int Index { get; set; }
    public int Depth { get; set; }
    public string Type { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Value { get; set; } = "0";
    public string Gas { get; set; } = "0";
    public string GasUsed { get; set; } = "0";
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? RevertReason { get; set; }
    public bool IsError => !string.IsNullOrEmpty(Error);
}

public class StateDiffResult
{
    public List<StateDiffEntry> Entries { get; set; } = new();
    public string? Error { get; set; }
}

public class StateDiffEntry
{
    public string Address { get; set; } = "";
    public string? BalanceBefore { get; set; }
    public string? BalanceAfter { get; set; }
    public long? NonceBefore { get; set; }
    public long? NonceAfter { get; set; }
    public bool CodeChanged { get; set; }
    public List<StorageDiff> StorageChanges { get; set; } = new();
}

public class StorageDiff
{
    public string Slot { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
}

public class AccountSummary
{
    public string Address { get; set; } = "";
    public int TransactionCount { get; set; }
    public bool IsContract { get; set; }
}
