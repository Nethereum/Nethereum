using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockStorage
{
    public class BlockStorageProcessorTests : ProcessingTestBase
    {

        [Fact]
        public async Task Crawls_Blocks_And_Persists_To_Repositories()
        {
            var rpcMock = new BlockProcessingRpcMock(Web3Mock);
            rpcMock.SetupGetCurrentBlockNumber(200);
            rpcMock.SetupTransactionsWithReceipts(100, 10, 2);
            rpcMock.SetupTransactionsWithReceipts(101, 10, 2);

            var context = new InMemoryBlockchainStorageRepositoryContext();
            var repoFactory = new InMemoryBlockchainStoreRepositoryFactory(context);

            var processor = Web3.Processing.Blocks.CreateBlockStorageProcessor(repoFactory);

            var cancellationToken = new CancellationToken();

            //crawl the required block range
            await processor.ExecuteAsync(
                toBlockNumber: new BigInteger(101),
                cancellationToken: cancellationToken,
                startAtBlockNumberIfNotProcessed: new BigInteger(100));

            Assert.Equal(2, context.Blocks.Count);
            Assert.Equal(20, context.Transactions.Count);
            Assert.Equal(20, context.AddressTransactions.Count);
            Assert.Equal(40, context.TransactionLogs.Count);
        }

    }
}


