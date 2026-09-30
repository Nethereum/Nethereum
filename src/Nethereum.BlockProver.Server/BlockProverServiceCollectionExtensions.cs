using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.BlockProver.Server
{
    public static class BlockProverServiceCollectionExtensions
    {
        public static IServiceCollection AddBlockProverOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BlockProverOptions>(configuration.GetSection("BlockProver"));
            return services;
        }
    }
}
