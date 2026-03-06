using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.ABIRepository;
using Nethereum.Blazor.Solidity.Services;
using Nethereum.Web3;

namespace Nethereum.Blazor.Solidity;

public static class SolidityServiceCollectionExtensions
{
    public static IServiceCollection AddSolidityDebugger(this IServiceCollection services)
    {
        services.AddScoped<IEvmDebugService>(sp =>
            new EvmDebugService(
                sp.GetRequiredService<IWeb3>(),
                sp.GetRequiredService<IABIInfoStorage>(),
                sp.GetService<FileSystemABIInfoStorage>(),
                sp.GetService<ILogger<EvmDebugService>>()));
        return services;
    }
}
