using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.BlockchainStorage.Processors
{
    public static class BlockchainProcessorServiceCollectionExtensions
    {
        public static IServiceCollection AddBlockchainProcessingOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var optionsSection = configuration.GetSection("BlockchainProcessing");
            services.AddOptions<BlockchainProcessingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var source = optionsSection.Exists()
                        ? BlockchainProcessingOptions.Load(optionsSection)
                        : BlockchainProcessingOptions.Load(config);

                    options.BlockchainUrl = source.BlockchainUrl;
                    options.Name = source.Name;
                    options.MinimumBlockNumber = source.MinimumBlockNumber;
                    options.MinimumBlockConfirmations = source.MinimumBlockConfirmations;
                    options.FromBlock = source.FromBlock;
                    options.ToBlock = source.ToBlock;
                    options.PostVm = source.PostVm;
                    options.ProcessBlockTransactionsInParallel = source.ProcessBlockTransactionsInParallel;
                    options.NumberOfBlocksToProcessPerRequest = source.NumberOfBlocksToProcessPerRequest;
                    options.RetryWeight = source.RetryWeight;
                    options.ReorgBuffer = source.ReorgBuffer;
                    options.UseBatchReceipts = source.UseBatchReceipts;
                });

            return services;
        }

        public static IServiceCollection AddBlockchainProcessor(
            this IServiceCollection services)
        {
            services.AddTransient<BlockchainProcessingService>();
            services.AddHostedService<BlockchainProcessingHostedService>();
            return services;
        }

        public static IServiceCollection AddInternalTransactionProcessor(
            this IServiceCollection services)
        {
            services.AddTransient<InternalTransactionProcessingService>();
            services.AddHostedService<InternalTransactionProcessingHostedService>();
            return services;
        }
    }
}
