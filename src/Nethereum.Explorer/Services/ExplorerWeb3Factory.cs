using Microsoft.Extensions.Options;

namespace Nethereum.Explorer.Services;

public class ExplorerWeb3Factory
{
    private readonly ExplorerOptions _options;
    private readonly IConfiguration _configuration;
    private readonly object _lock = new();
    private Nethereum.Web3.Web3? _web3;

    public ExplorerWeb3Factory(IOptions<ExplorerOptions> options, IConfiguration configuration)
    {
        _options = options.Value;
        _configuration = configuration;
    }

    public string? GetRpcUrl() =>
        _options.RpcUrl ?? _configuration.GetConnectionString("devchain");

    public Nethereum.Web3.Web3? GetWeb3()
    {
        var rpcUrl = GetRpcUrl();
        if (string.IsNullOrEmpty(rpcUrl)) return null;
        if (_web3 != null) return _web3;
        lock (_lock)
        {
            return _web3 ??= new Nethereum.Web3.Web3(rpcUrl);
        }
    }

    public bool IsAvailable => !string.IsNullOrEmpty(GetRpcUrl());
}
