using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Explorer.Services;

public static class ExplorerFormatUtils
{
    public static string FormatBlockNumber(string? blockNumber)
    {
        if (string.IsNullOrEmpty(blockNumber)) return "0";
        return blockNumber.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";
    }

    public static string TruncateHex(string? value, int maxLength = 16)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= maxLength) return value;
        var half = (maxLength - 3) / 2;
        return $"{value[..half]}...{value[^half..]}";
    }

    public static string FormatAddress(string? address)
    {
        if (string.IsNullOrEmpty(address)) return "";
        return address.ConvertToEthereumChecksumAddress();
    }

    public static string FormatAddressShort(string? address, int maxLength = 14)
    {
        return TruncateHex(FormatAddress(address), maxLength);
    }

    public static string FormatHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash)) return "";
        return hash.EnsureHexPrefix();
    }

    public static string FormatHashShort(string? hash, int maxLength = 18)
    {
        return TruncateHex(FormatHash(hash), maxLength);
    }

    public static string FormatEth(string? weiValue)
    {
        var wei = ParseBigInteger(weiValue);
        if (wei == BigInteger.Zero) return "0";
        var eth = UnitConversion.Convert.FromWei(wei);
        return eth == 0 ? "0" : eth.ToString("0.######");
    }

    public static string FormatEthFull(string? weiValue)
    {
        var wei = ParseBigInteger(weiValue);
        if (wei == BigInteger.Zero) return "0";
        return UnitConversion.Convert.FromWei(wei).ToString();
    }

    public static string FormatGwei(string? weiValue)
    {
        var wei = ParseBigInteger(weiValue);
        if (wei == BigInteger.Zero) return "0";
        return UnitConversion.Convert.FromWei(wei, UnitConversion.EthUnit.Gwei).ToString();
    }

    public static BigInteger ComputeFee(string? gasUsed, string? gasPrice)
    {
        return ParseBigInteger(gasUsed) * ParseBigInteger(gasPrice);
    }

    public static string FormatGas(string? gas)
    {
        var val = ParseLong(gas);
        if (val == 0) return "0";
        if (val >= 1_000_000) return $"{val / 1_000_000.0:F1}M";
        if (val >= 1_000) return $"{val / 1_000.0:F1}K";
        return val.ToString("N0");
    }

    public static int GasPercentage(string? used, string? limit)
    {
        var u = ParseLong(used);
        var l = ParseLong(limit);
        if (l == 0) return 0;
        return (int)(u * 100 / l);
    }

    public static string GasColor(int percentage)
    {
        return percentage switch
        {
            >= 80 => "var(--danger-color)",
            >= 50 => "var(--warning-color)",
            _ => "var(--success-color)"
        };
    }

    public static string FormatNumber(string? val)
    {
        var n = ParseLong(val);
        return n.ToString("N0");
    }

    public static string FormatAge(long timestamp)
    {
        if (timestamp == 0) return "";
        var unix = timestamp;
        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (age.TotalSeconds < 60) return $"{(int)age.TotalSeconds}s ago";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours}h {(int)(age.TotalMinutes % 60)}m ago";
        return $"{(int)age.TotalDays}d ago";
    }

    public static string FormatTimestamp(long timestamp)
    {
        if (timestamp == 0) return "";
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }

    public static string FormatTxType(long val)
    {
        return val switch
        {
            0 => "0 (Legacy)",
            1 => "1 (EIP-2930)",
            2 => "2 (EIP-1559)",
            3 => "3 (EIP-4844)",
            4 => "4 (EIP-7702)",
            _ => val.ToString()
        };
    }

    public static bool IsContractCreation(string? addressTo)
    {
        return string.IsNullOrEmpty(addressTo);
    }

    public static bool IsAddressMatch(string? address1, string? address2)
    {
        if (string.IsNullOrEmpty(address1) || string.IsNullOrEmpty(address2)) return false;
        return address1.IsTheSameAddress(address2);
    }

    public static BigInteger ParseBigInteger(string? value)
    {
        if (string.IsNullOrEmpty(value)) return BigInteger.Zero;
        if (value.HasHexPrefix())
            return value.HexToBigInteger(false);
        return BigInteger.TryParse(value, out var result) ? result : BigInteger.Zero;
    }

    public static long ParseLong(string? val)
    {
        if (string.IsNullOrEmpty(val)) return 0;
        try
        {
            if (val.HasHexPrefix())
            {
                var big = val.HexToBigInteger(false);
                return big > long.MaxValue ? long.MaxValue : (long)big;
            }
        }
        catch { return 0; }
        return long.TryParse(val, out var n) ? n : 0;
    }

    public const string ERC20_TRANSFER_TOPIC = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    public static string ExtractAddressFromTopic(string? topic)
    {
        if (string.IsNullOrEmpty(topic)) return "";
        var hex = topic.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? topic[2..] : topic;
        if (hex.Length < 40) return "";
        return ("0x" + hex[^40..]).ConvertToEthereumChecksumAddress();
    }

    public static bool IsErc20Transfer(string? eventHash)
    {
        if (string.IsNullOrEmpty(eventHash)) return false;
        return string.Equals(eventHash, ERC20_TRANSFER_TOPIC, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatTokenAmount(string? rawValue, int decimals = 18)
    {
        var val = ParseBigInteger(rawValue);
        if (val == BigInteger.Zero) return "0";
        return UnitConversion.Convert.FromWei(val, decimals).ToString("0.######");
    }

    public static string DecodeHexToUtf8(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "0x") return "";
        try
        {
            return hex.HexToUTF8String();
        }
        catch
        {
            return "";
        }
    }

    public static string GetMethodSelector(string? input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 10) return "";
        return input[..10].ToLowerInvariant();
    }

    public static string GetMethodName(string? input)
    {
        if (string.IsNullOrEmpty(input) || input == "0x") return "Transfer";
        var selector = GetMethodSelector(input);
        if (string.IsNullOrEmpty(selector)) return "";
        return KnownMethodSelectors.TryGetValue(selector, out var name) ? name : selector;
    }

    public static string GetMethodBadgeColor(string? input)
    {
        if (string.IsNullOrEmpty(input) || input == "0x")
            return "rgba(100, 255, 218, 0.15)";
        var selector = GetMethodSelector(input);
        return KnownMethodSelectors.ContainsKey(selector)
            ? "rgba(100, 255, 218, 0.15)"
            : "rgba(168, 178, 209, 0.15)";
    }

    public static string GetMethodBadgeCssClass(string? input)
    {
        if (string.IsNullOrEmpty(input) || input == "0x")
            return "explorer-method-badge explorer-method-badge-known";
        var selector = GetMethodSelector(input);
        return KnownMethodSelectors.ContainsKey(selector)
            ? "explorer-method-badge explorer-method-badge-known"
            : "explorer-method-badge explorer-method-badge-unknown";
    }

    public static BigInteger? CalculateBurnedFees(string? baseFeePerGas, string? gasUsed)
    {
        var baseFee = ParseBigInteger(baseFeePerGas);
        var gas = ParseBigInteger(gasUsed);
        if (baseFee == BigInteger.Zero || gas == BigInteger.Zero) return null;
        return baseFee * gas;
    }

    public static string FormatWeiToEth(BigInteger wei)
    {
        if (wei == BigInteger.Zero) return "0";
        var eth = UnitConversion.Convert.FromWei(wei);
        return eth.ToString("0.############");
    }

    public static string HexToDecimalString(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return "0";
        try
        {
            var clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
            if (string.IsNullOrEmpty(clean)) return "0";
            return BigInteger.Parse("0" + clean, System.Globalization.NumberStyles.HexNumber).ToString();
        }
        catch
        {
            return "0";
        }
    }

    public static string GetTokenTypeBadgeColor(string? tokenType) => tokenType switch
    {
        "ERC20" => "rgba(100, 255, 218, 0.15)",
        "ERC721" => "rgba(168, 85, 247, 0.15)",
        "ERC1155" => "rgba(251, 191, 36, 0.15)",
        _ => "rgba(148, 163, 184, 0.15)"
    };

    public static string GetTokenTypeBadgeCssClass(string? tokenType) => tokenType switch
    {
        "ERC20" => "explorer-badge explorer-badge-erc20",
        "ERC721" => "explorer-badge explorer-badge-erc721",
        "ERC1155" => "explorer-badge explorer-badge-erc1155",
        _ => "explorer-badge explorer-badge-neutral"
    };

    public static string GasColorCssClass(int percentage) => percentage switch
    {
        >= 80 => "text-color-danger",
        >= 50 => "text-color-warning",
        _ => "text-color-success"
    };

    private static readonly Dictionary<string, string> KnownMethodSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0xa9059cbb"] = "transfer",
        ["0x23b872dd"] = "transferFrom",
        ["0x095ea7b3"] = "approve",
        ["0x40c10f19"] = "mint",
        ["0x42966c68"] = "burn",
        ["0xa0712d68"] = "mint",
        ["0x79cc6790"] = "burnFrom",
        ["0xd0def521"] = "mint",
        ["0x42842e0e"] = "safeTransferFrom",
        ["0xb88d4fde"] = "safeTransferFrom",
        ["0xf242432a"] = "safeTransferFrom",
        ["0x2eb2c2d6"] = "safeBatchTransferFrom",
        ["0xa22cb465"] = "setApprovalForAll",
        ["0x3593564c"] = "execute",
        ["0x3ccfd60b"] = "withdraw",
        ["0xd0e30db0"] = "deposit",
        ["0x70a08231"] = "balanceOf",
        ["0x18160ddd"] = "totalSupply",
        ["0x06fdde03"] = "name",
        ["0x95d89b41"] = "symbol",
        ["0x313ce567"] = "decimals",
        ["0xdd62ed3e"] = "allowance",
        ["0x5c975abb"] = "paused",
        ["0x8456cb59"] = "pause",
        ["0x3f4ba83a"] = "unpause",
        ["0x715018a6"] = "renounceOwnership",
        ["0xf2fde38b"] = "transferOwnership",
        ["0x8da5cb5b"] = "owner",
        ["0x2e1a7d4d"] = "withdraw",
        ["0xdb006a75"] = "redeem",
        ["0xb6b55f25"] = "deposit",
        ["0xe8e33700"] = "addLiquidity",
        ["0x38ed1739"] = "swapExactTokensForTokens",
        ["0x7ff36ab5"] = "swapExactETHForTokens",
        ["0x18cbafe5"] = "swapExactTokensForETH",
        ["0xfb3bdb41"] = "swapETHForExactTokens",
        ["0x5ae401dc"] = "multicall",
        ["0xac9650d8"] = "multicall",
        ["0x1fad948c"] = "handleOps",
        ["0x765e827f"] = "handleAggregatedOps",
    };
}
