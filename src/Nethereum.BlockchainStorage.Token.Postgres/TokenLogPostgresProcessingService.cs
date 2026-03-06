using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.BlockchainStorage.Token.Postgres.Repositories;
using Nethereum.Web3;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public class TokenLogPostgresProcessingService
    {
        public TokenLogPostgresProcessingService(
            TokenPostgresDbContext context,
            ILogger<TokenLogPostgresProcessingService> logger)
        {
            Context = context;
            Logger = logger;
        }

        public TokenPostgresDbContext Context { get; }
        public ILogger Logger { get; set; }
        public string RpcUrl { get; set; }
        public string[] ContractAddresses { get; set; }
        public BigInteger StartAtBlockNumberIfNotProcessed { get; set; } = 0;
        public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
        public uint MinimumNumberOfConfirmations { get; set; } = 0;
        public int ReorgBuffer { get; set; } = 0;
        public ILogProcessingObserver Observer { get; set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var web3 = new Web3.Web3(RpcUrl);
            var repository = new TokenPostgresTransactionLogRepository(Context);
            IBlockProgressRepository progressRepository = new TokenPostgresBlockProgressRepository(Context);

            if (ReorgBuffer > 0)
            {
                progressRepository = new ReorgBufferedBlockProgressRepository(progressRepository, ReorgBuffer);
            }

            var blockchainLogProcessing = new BlockchainLogProcessingService(web3.Eth);
            var tokenLogService = new TokenTransferLogProcessingService(
                blockchainLogProcessing, web3.Eth);

            var processor = tokenLogService.CreateProcessorForTransactionLogStorage(
                repository,
                progressRepository,
                Logger,
                NumberOfBlocksToProcessPerRequest,
                RetryWeight,
                MinimumNumberOfConfirmations,
                ReorgBuffer,
                Observer,
                ContractAddresses);

            await processor.ExecuteAsync(
                cancellationToken: cancellationToken,
                startAtBlockNumberIfNotProcessed: StartAtBlockNumberIfNotProcessed);
        }
    }
}
