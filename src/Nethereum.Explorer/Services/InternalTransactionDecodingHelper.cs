using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public class InternalTransactionDecodingHelper
{
    private readonly IAbiDecodingService _abiDecoding;
    private readonly ILogger<InternalTransactionDecodingHelper> _logger;
    private readonly ConcurrentDictionary<string, DecodedFunctionCall?> _cache = new();

    public InternalTransactionDecodingHelper(IAbiDecodingService abiDecoding, ILogger<InternalTransactionDecodingHelper> logger)
    {
        _abiDecoding = abiDecoding;
        _logger = logger;
    }

    public void ClearCache() => _cache.Clear();

    public async Task DecodeAsync(IEnumerable<IInternalTransactionView> transactions)
    {
        foreach (var itx in transactions)
        {
            if (string.IsNullOrEmpty(itx.Input) || itx.Input == "0x" || itx.Input.Length < 10) continue;
            var target = itx.AddressTo;
            if (string.IsNullOrEmpty(target)) continue;
            var cacheKey = $"{target}_{itx.Input[..10]}";
            if (_cache.ContainsKey(cacheKey)) continue;
            try
            {
                var decoded = await _abiDecoding.DecodeFunctionInputAsync(target, itx.Input);
                _cache.TryAdd(cacheKey, decoded);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to decode internal tx input for {Address}", target);
                _cache.TryAdd(cacheKey, null);
            }
        }
    }

    public string GetMethodName(IInternalTransactionView itx)
    {
        var decoded = GetDecodedFunction(itx);
        if (decoded != null) return decoded.FunctionName;
        return ExplorerFormatUtils.GetMethodName(itx.Input);
    }

    public DecodedFunctionCall? GetDecodedFunction(IInternalTransactionView itx)
    {
        if (string.IsNullOrEmpty(itx.Input) || itx.Input == "0x" || itx.Input.Length < 10)
            return null;
        var target = itx.AddressTo;
        if (string.IsNullOrEmpty(target)) return null;
        var cacheKey = $"{target}_{itx.Input[..10]}";
        _cache.TryGetValue(cacheKey, out var decoded);
        return decoded;
    }

    public static string GetCallTypeBadgeColor(string? type) => type switch
    {
        "CALL" => "rgba(56, 189, 248, 0.15)",
        "DELEGATECALL" => "rgba(168, 85, 247, 0.15)",
        "STATICCALL" => "rgba(148, 163, 184, 0.15)",
        "CREATE" or "CREATE2" => "rgba(100, 255, 218, 0.15)",
        "CALLCODE" => "rgba(251, 191, 36, 0.15)",
        _ => "rgba(148, 163, 184, 0.15)"
    };
}
