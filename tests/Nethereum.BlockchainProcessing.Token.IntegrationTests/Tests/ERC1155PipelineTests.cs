using System.Numerics;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.BlockchainProcessing.Token.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.BlockchainProcessing.Token.IntegrationTests.Tests
{
    [Collection("TokenPipeline")]
    public class ERC1155PipelineTests
    {
        private readonly TokenPipelineFixture _fixture;

        public ERC1155PipelineTests(TokenPipelineFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(InMemoryTokenTransferLogRepository transferLogRepo,
            InMemoryTokenBalanceRepository balanceRepo,
            InMemoryNFTInventoryRepository nftRepo)> RunPipeline(string[]? contractAddresses = null)
        {
            var transferLogRepo = new InMemoryTokenTransferLogRepository();
            var balanceRepo = new InMemoryTokenBalanceRepository();
            var nftRepo = new InMemoryNFTInventoryRepository();
            var progressRepo = new InMemoryBlockchainProgressRepository();

            var logProcessing = new BlockchainLogProcessingService(_fixture.Web3.Eth);
            var transferService = new TokenTransferLogProcessingService(logProcessing, _fixture.Web3.Eth);
            await transferService.ProcessAllTransferLogsAsync(
                transferLogRepo, fromBlockNumber: 0, toBlockNumber: null,
                CancellationToken.None, numberOfBlocksPerRequest: 100,
                contractAddresses: contractAddresses);

            var aggregator = new TokenBalanceAggregationService(
                transferLogRepo, balanceRepo, nftRepo, progressRepo);
            await aggregator.ProcessTransferLogsAsync(transferLogRepo.Records);

            return (transferLogRepo, balanceRepo, nftRepo);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC1155Mints_When_Processed_Then_LogsHaveTypeERC1155()
        {
            var (transferLogRepo, _, _) = await RunPipeline(new[] { _fixture.ERC1155Address });

            var erc1155Logs = transferLogRepo.Records
                .Where(r => r.TokenType == "ERC1155")
                .ToList();

            Assert.Equal(3, erc1155Logs.Count);
            Assert.All(erc1155Logs, log =>
            {
                Assert.Equal("ERC1155", log.TokenType);
                Assert.NotNull(log.TokenId);
                Assert.NotNull(log.Amount);
                Assert.NotNull(log.OperatorAddress);
            });
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC1155MintAndTransfer_When_Aggregated_Then_NFTAmountsCorrect()
        {
            // Mints: Address gets id=1(100), id=2(50)
            // Transfer: Address->Address2, id=1, amount=30
            // Result: Address id1=70,id2=50; Address2 id1=30
            var (_, _, nftRepo) = await RunPipeline(new[] { _fixture.ERC1155Address });

            var addr1Nfts = (await nftRepo.GetByAddressAsync(_fixture.Address))
                .Where(n => n.ContractAddress?.ToLowerInvariant() == _fixture.ERC1155Address.ToLowerInvariant())
                .ToList();

            var addr1Id1 = addr1Nfts.First(n => n.TokenId == "1");
            var addr1Id2 = addr1Nfts.First(n => n.TokenId == "2");
            Assert.Equal("70", addr1Id1.Amount);
            Assert.Equal("50", addr1Id2.Amount);

            var addr2Nfts = (await nftRepo.GetByAddressAsync(_fixture.Address2))
                .Where(n => n.ContractAddress?.ToLowerInvariant() == _fixture.ERC1155Address.ToLowerInvariant())
                .ToList();

            Assert.Single(addr2Nfts);
            Assert.Equal("1", addr2Nfts[0].TokenId);
            Assert.Equal("30", addr2Nfts[0].Amount);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC1155Mint_When_Processed_Then_ZeroAddressNotDebited()
        {
            var (_, _, nftRepo) = await RunPipeline(new[] { _fixture.ERC1155Address });

            var zeroAddrNfts = (await nftRepo.GetByAddressAsync("0x0000000000000000000000000000000000000000")).ToList();
            Assert.Empty(zeroAddrNfts);
        }
    }
}
