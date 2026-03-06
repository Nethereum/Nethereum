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
    public class ERC721PipelineTests
    {
        private readonly TokenPipelineFixture _fixture;

        public ERC721PipelineTests(TokenPipelineFixture fixture)
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
        public async Task Given_ERC721Mints_When_Processed_Then_LogsHaveTokenIdAndTypeERC721()
        {
            var (transferLogRepo, _, _) = await RunPipeline(new[] { _fixture.ERC721Address });

            var erc721Logs = transferLogRepo.Records
                .Where(r => r.TokenType == "ERC721")
                .ToList();

            Assert.Equal(4, erc721Logs.Count);
            Assert.All(erc721Logs, log =>
            {
                Assert.Equal("ERC721", log.TokenType);
                Assert.NotNull(log.TokenId);
                Assert.Null(log.Amount);
            });
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC721MintAndTransfer_When_Aggregated_Then_NFTInventoryCorrect()
        {
            // After mints: Address owns 0,1; Address2 owns 2
            // After transfer(Address->Address2, tokenId=0): Address owns 1; Address2 owns 0,2
            var (_, _, nftRepo) = await RunPipeline(new[] { _fixture.ERC721Address });

            var addr1Nfts = (await nftRepo.GetByAddressAsync(_fixture.Address))
                .Where(n => n.Amount != "0")
                .ToList();
            var addr2Nfts = (await nftRepo.GetByAddressAsync(_fixture.Address2))
                .Where(n => n.Amount != "0")
                .ToList();

            Assert.Single(addr1Nfts);
            Assert.Equal("1", addr1Nfts[0].TokenId);

            Assert.Equal(2, addr2Nfts.Count);
            var addr2TokenIds = addr2Nfts.Select(n => n.TokenId).OrderBy(t => t).ToList();
            Assert.Equal("0", addr2TokenIds[0]);
            Assert.Equal("2", addr2TokenIds[1]);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC721Transfer_When_Processed_Then_PreviousOwnerInventoryZeroed()
        {
            var (_, _, nftRepo) = await RunPipeline(new[] { _fixture.ERC721Address });

            // Address originally owned token 0, then transferred it. Entry should be "0".
            var addr1Token0 = nftRepo.Records
                .FirstOrDefault(r =>
                    r.Address?.ToLowerInvariant() == _fixture.Address.ToLowerInvariant() &&
                    r.TokenId == "0" &&
                    r.ContractAddress?.ToLowerInvariant() == _fixture.ERC721Address.ToLowerInvariant());

            Assert.NotNull(addr1Token0);
            Assert.Equal("0", addr1Token0!.Amount);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC721Transfers_When_Aggregated_Then_BalanceCountsCorrect()
        {
            var (_, balanceRepo, _) = await RunPipeline(new[] { _fixture.ERC721Address });

            var addr1Balances = (await balanceRepo.GetByAddressAsync(_fixture.Address)).ToList();
            var addr2Balances = (await balanceRepo.GetByAddressAsync(_fixture.Address2)).ToList();

            var addr1Erc721 = addr1Balances.FirstOrDefault(b =>
                b.ContractAddress?.ToLowerInvariant() == _fixture.ERC721Address.ToLowerInvariant());
            var addr2Erc721 = addr2Balances.FirstOrDefault(b =>
                b.ContractAddress?.ToLowerInvariant() == _fixture.ERC721Address.ToLowerInvariant());

            Assert.NotNull(addr1Erc721);
            Assert.Equal(BigInteger.One, BigInteger.Parse(addr1Erc721!.Balance!));
            Assert.NotNull(addr2Erc721);
            Assert.Equal(new BigInteger(2), BigInteger.Parse(addr2Erc721!.Balance!));
        }
    }
}
