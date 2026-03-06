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
    public class MixedPipelineTests
    {
        private readonly TokenPipelineFixture _fixture;

        public MixedPipelineTests(TokenPipelineFixture fixture)
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
        public async Task Given_AllTokenTypes_When_Processed_Then_AllLogsStored()
        {
            var (transferLogRepo, _, _) = await RunPipeline();

            var erc20Count = transferLogRepo.Records.Count(r => r.TokenType == "ERC20");
            var erc721Count = transferLogRepo.Records.Count(r => r.TokenType == "ERC721");
            var erc1155Count = transferLogRepo.Records.Count(r => r.TokenType == "ERC1155");

            Assert.Equal(3, erc20Count);
            Assert.Equal(4, erc721Count);
            Assert.Equal(3, erc1155Count);
            Assert.Equal(10, transferLogRepo.Records.Count);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_AllTokenTypes_When_Aggregated_Then_EachTypeBalancedIndependently()
        {
            var (_, balanceRepo, nftRepo) = await RunPipeline();

            // ERC20 balances
            var addr1Erc20 = (await balanceRepo.GetByAddressAsync(_fixture.Address))
                .First(b => b.ContractAddress?.ToLowerInvariant() == _fixture.ERC20Address.ToLowerInvariant());
            Assert.Equal(BigInteger.Parse("850000000000000000000"), BigInteger.Parse(addr1Erc20.Balance!));

            // ERC721 balances
            var addr1Erc721 = (await balanceRepo.GetByAddressAsync(_fixture.Address))
                .First(b => b.ContractAddress?.ToLowerInvariant() == _fixture.ERC721Address.ToLowerInvariant());
            Assert.Equal(BigInteger.One, BigInteger.Parse(addr1Erc721.Balance!));

            // ERC1155 NFT inventory
            var addr1Id1 = nftRepo.Records
                .First(n => n.Address?.ToLowerInvariant() == _fixture.Address.ToLowerInvariant() &&
                            n.TokenId == "1" &&
                            n.ContractAddress?.ToLowerInvariant() == _fixture.ERC1155Address.ToLowerInvariant());
            Assert.Equal("70", addr1Id1.Amount);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_AllTokenTypes_When_FilteredByContract_Then_OnlyMatchingLogs()
        {
            var (transferLogRepo, _, _) = await RunPipeline(new[] { _fixture.ERC20Address });

            Assert.All(transferLogRepo.Records, log =>
            {
                Assert.Equal("ERC20", log.TokenType);
                Assert.Equal(_fixture.ERC20Address.ToLowerInvariant(), log.ContractAddress?.ToLowerInvariant());
            });
            Assert.Equal(3, transferLogRepo.Records.Count);
        }
    }
}
