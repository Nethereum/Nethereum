namespace Nethereum.Explorer.Services;

public class ExplorerOptions
{
    public string? RpcUrl { get; set; }
    public string ChainName { get; set; } = "DevChain";
    public string CurrencySymbol { get; set; } = "ETH";
    public string CurrencyName { get; set; } = "Ether";
    public uint CurrencyDecimals { get; set; } = 18;
    public string? BlockExplorerUrl { get; set; }
    public long ChainId { get; set; } = 31337;

    public string ExplorerTitle { get; set; } = "Nethereum Explorer";
    public string ExplorerBrandName { get; set; } = "Nethereum";
    public string ExplorerBrandSuffix { get; set; } = "Explorer";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }

    public string? ApiKey { get; set; }

    public bool EnableMud { get; set; } = true;
    public bool EnableTokens { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public bool EnableInternalTransactions { get; set; } = true;
    public bool EnablePendingTransactions { get; set; } = false;
    public bool EnableEvmDebugger { get; set; } = true;

    public int RpcRequestTimeoutSeconds { get; set; } = 30;

    public AbiSourceOptions AbiSources { get; set; } = new();
}

public class AbiSourceOptions
{
    public bool SourcifyEnabled { get; set; } = true;
    public bool FourByteEnabled { get; set; } = false;
    public bool EtherscanEnabled { get; set; } = false;
    public string? EtherscanApiKey { get; set; }
    public bool LocalStorageEnabled { get; set; } = false;
    public string? LocalStoragePath { get; set; }
    public string? SourceBasePath { get; set; }
}
