using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.HostWallet;
using Nethereum.UI;
using Nethereum.Web3;

namespace Nethereum.Explorer.Services;

public class ExplorerChainService
{
    private readonly ExplorerOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExplorerChainService> _logger;
    private readonly SemaphoreSlim _chainIdLock = new(1, 1);
    private HexBigInteger? _chainId;
    private bool _chainIdFetched;

    public ExplorerChainService(IOptions<ExplorerOptions> options, IServiceProvider serviceProvider, ILogger<ExplorerChainService> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string? RpcUrl => _options.RpcUrl;
    public string ChainName => !string.IsNullOrEmpty(_options.ChainName) ? _options.ChainName : $"Chain {ChainId?.Value}";
    public string CurrencySymbol => _options.CurrencySymbol;
    public string CurrencyName => _options.CurrencyName;
    public uint CurrencyDecimals => _options.CurrencyDecimals;
    public string? BlockExplorerUrl => _options.BlockExplorerUrl;

    public HexBigInteger? ChainId => _chainId;

    public async Task<HexBigInteger?> GetChainIdAsync()
    {
        if (_chainIdFetched) return _chainId;

        await _chainIdLock.WaitAsync();
        try
        {
            if (_chainIdFetched) return _chainId;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var web3 = scope.ServiceProvider.GetRequiredService<IWeb3>();
                _chainId = await web3.Eth.ChainId.SendRequestAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch chain ID from RPC");
            }

            _chainIdFetched = true;
        }
        finally
        {
            _chainIdLock.Release();
        }
        return _chainId;
    }

    public AddEthereumChainParameter BuildAddChainParameter()
    {
        var param = new AddEthereumChainParameter
        {
            ChainId = _chainId ?? new HexBigInteger(_options.ChainId),
            ChainName = ChainName,
            NativeCurrency = new NativeCurrency
            {
                Name = CurrencyName,
                Symbol = CurrencySymbol,
                Decimals = CurrencyDecimals
            }
        };

        if (!string.IsNullOrEmpty(RpcUrl))
            param.RpcUrls = new List<string> { RpcUrl };

        if (!string.IsNullOrEmpty(BlockExplorerUrl))
            param.BlockExplorerUrls = new List<string> { BlockExplorerUrl };

        return param;
    }

    public async Task<bool> AddChainToWalletAsync(SelectedEthereumHostProviderService hostProvider)
    {
        if (hostProvider?.SelectedHost == null)
            return false;

        try
        {
            if (_chainId == null)
                await GetChainIdAsync();

            var web3 = await hostProvider.SelectedHost.GetWeb3Async();
            await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(BuildAddChainParameter());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add chain to wallet");
            return false;
        }
    }

}
