using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIRepository;
using Nethereum.Blazor;
using Nethereum.Blazor.EIP6963WalletInterop;
using Nethereum.Explorer.Services.Localization;
using Nethereum.Blazor.Storage;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.DataServices.ABIInfoStorage;
using Nethereum.EIP6963WalletInterop;
using Nethereum.UI;
using Nethereum.Blazor.Solidity;
using Nethereum.Web3;

namespace Nethereum.Explorer.Services;

public static class ExplorerServiceCollectionExtensions
{
    public static IServiceCollection AddExplorerServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExplorerOptions>(configuration.GetSection("Explorer"));
        services.AddSingleton<ExplorerLocalizer>();
        services.AddSingleton<ExplorerWeb3Factory>();
        services.AddScoped<IWeb3>(sp => sp.GetRequiredService<ExplorerWeb3Factory>().GetWeb3()!);
        services.AddScoped<RecentSearchService>();
        services.AddScoped<ToastService>();
        services.AddScoped<LocalStorageHelper>();

        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<SelectedEthereumHostProviderService>();
        services.AddScoped<IEIP6963WalletInterop, EIP6963WalletBlazorInterop>();
        services.AddScoped<EIP6963WalletHostProvider>();
        services.AddScoped<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();

        services.AddScoped<IBlockQueryService, BlockQueryService>();
        services.AddScoped<ITransactionQueryService, TransactionQueryService>();
        services.AddScoped<IContractQueryService, ContractQueryService>();
        services.AddScoped<IAccountQueryService, AccountQueryService>();
        services.AddScoped<ILogQueryService, LogQueryService>();
        services.AddScoped<IInternalTransactionQueryService, InternalTransactionQueryService>();
        services.AddScoped<IRpcQueryService, RpcQueryService>();
        services.AddScoped<ITransactionTraceService, TransactionTraceService>();
        services.AddSolidityDebugger();
        services.AddScoped<ISearchResolverService, SearchResolverService>();
        services.AddScoped<ExplorerChainService>();

        services.AddSingleton<ABIInfoInMemoryStorage>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ExplorerOptions>>().Value;
            var abiSources = options.AbiSources;
            return new FileSystemABIInfoStorage(
                abiSources.LocalStoragePath ?? "",
                abiSources.SourceBasePath);
        });
        services.AddSingleton<IABIInfoStorage>(sp =>
        {
            var cache = sp.GetRequiredService<ABIInfoInMemoryStorage>();
            var options = sp.GetRequiredService<IOptions<ExplorerOptions>>().Value;
            var abiSources = options.AbiSources;

            var implementations = new List<IABIInfoStorage>();

            if (abiSources.LocalStorageEnabled && !string.IsNullOrEmpty(abiSources.LocalStoragePath))
            {
                implementations.Insert(0, sp.GetRequiredService<FileSystemABIInfoStorage>());
            }

            if (abiSources.SourcifyEnabled)
                implementations.Add(new Nethereum.DataServices.ABIInfoStorage.SourcifyABIInfoStorage());

            if (abiSources.EtherscanEnabled && !string.IsNullOrEmpty(abiSources.EtherscanApiKey))
                implementations.Add(new Nethereum.DataServices.ABIInfoStorage.EtherscanABIInfoStorage(abiSources.EtherscanApiKey));

            if (abiSources.FourByteEnabled)
            {
                implementations.Add(new Nethereum.DataServices.ABIInfoStorage.Sourcify4ByteABIInfoStorage());
                implementations.Add(new Nethereum.DataServices.ABIInfoStorage.FourByteDirectoryABIInfoStorage());
            }

            if (implementations.Count == 0)
                return ABIInfoStorageFactory.CreateLocalOnly();

            return ABIInfoStorageFactory.CreateCustom(
                cache,
                implementations.ToArray());
        });
        services.AddSingleton<AbiCacheService>();
        services.AddScoped<IAbiStorageService, AbiStorageService>();
        services.AddScoped<IAbiDecodingService, AbiDecodingService>();
        services.AddScoped<InternalTransactionDecodingHelper>();

        services.AddScoped<IMudExplorerService, MudExplorerService>();

        services.AddScoped<ITokenExplorerService>(sp =>
        {
            var balanceRepo = sp.GetService<ITokenBalanceRepository>();
            var nftRepo = sp.GetService<INFTInventoryRepository>();
            var transferRepo = sp.GetService<ITokenTransferLogRepository>();
            var metadataRepo = sp.GetService<ITokenMetadataRepository>();

            if (balanceRepo != null && nftRepo != null && transferRepo != null && metadataRepo != null)
                return new TokenExplorerService(balanceRepo, nftRepo, transferRepo, metadataRepo);

            return new NullTokenExplorerService();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("api", limiter =>
            {
                limiter.PermitLimit = 100;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });
        });

        return services;
    }
}
