using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Web3;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.Mud;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.Repositories.EntityFramework;

namespace Nethereum.Mud.Repositories.Postgres
{
    public class MudPostgresStoreRecordsProcessingService
    {
        public MudPostgresStoreRecordsProcessingService(MudPostgresStoreRecordsDbContext context, ILogger logger)
        {
            Context = context;
            Logger = logger;
        }

        public MudPostgresStoreRecordsDbContext Context { get; }
        public ILogger Logger { get; set;  }
        public string Address { get; set; }
        public string RpcUrl { get; set; }
        public BigInteger StartAtBlockNumberIfNotProcessed { get; set; }  = 0;
        public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;

        public uint MinimumNumberOfConfirmations { get; set; } = 0;

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var web3 = new Web3.Web3(RpcUrl);
            var progressRepository = new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(Context);
            var mudRepository = new MudPostgresStoreRecordsTableRepository(Context);
            var storeEventsLogProcessingService = new StoreEventsLogProcessingService(web3, Address);
            var processor = storeEventsLogProcessingService.CreateProcessor(mudRepository, progressRepository, Logger, NumberOfBlocksToProcessPerRequest, RetryWeight, MinimumNumberOfConfirmations);

            await processor.ExecuteAsync(
                cancellationToken: cancellationToken,
                startAtBlockNumberIfNotProcessed: StartAtBlockNumberIfNotProcessed);

        }
    }
}
