using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.Wallet.Services.VerifiedState
{
    public static class VerifiedStateServiceCollectionExtensions
    {
        public static IServiceCollection AddWalletVerifiedStateServices(this IServiceCollection services)
        {
            services.AddSingleton<IVerifiedBalanceService, VerifiedBalanceService>();
            return services;
        }
    }
}
