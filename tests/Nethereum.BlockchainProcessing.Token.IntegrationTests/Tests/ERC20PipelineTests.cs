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
    public class ERC20PipelineTests
    {
        private readonly TokenPipelineFixture _fixture;

        public ERC20PipelineTests(TokenPipelineFixture fixture)
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
        public async Task Given_ERC20Transfers_When_ProcessAllTransferLogs_Then_TransferLogsStored()
        {
            var (transferLogRepo, _, _) = await RunPipeline(new[] { _fixture.ERC20Address });

            var erc20Logs = transferLogRepo.Records
                .Where(r => r.TokenType == "ERC20")
                .ToList();

            Assert.Equal(3, erc20Logs.Count);
            Assert.All(erc20Logs, log =>
            {
                Assert.Equal("ERC20", log.TokenType);
                Assert.NotNull(log.Amount);
                Assert.NotNull(log.FromAddress);
                Assert.NotNull(log.ToAddress);
                Assert.Equal(_fixture.ERC20Address.ToLowerInvariant(), log.ContractAddress?.ToLowerInvariant());
            });
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_StoredERC20Logs_When_BalancesAggregated_Then_CorrectBalances()
        {
            var (_, balanceRepo, _) = await RunPipeline(new[] { _fixture.ERC20Address });

            var addr1Balances = (await balanceRepo.GetByAddressAsync(_fixture.Address)).ToList();
            var addr2Balances = (await balanceRepo.GetByAddressAsync(_fixture.Address2)).ToList();
            var addr3Balances = (await balanceRepo.GetByAddressAsync(_fixture.Address3)).ToList();

            var addr1Balance = addr1Balances.First(b => b.ContractAddress?.ToLowerInvariant() == _fixture.ERC20Address.ToLowerInvariant());
            var addr2Balance = addr2Balances.First(b => b.ContractAddress?.ToLowerInvariant() == _fixture.ERC20Address.ToLowerInvariant());
            var addr3Balance = addr3Balances.First(b => b.ContractAddress?.ToLowerInvariant() == _fixture.ERC20Address.ToLowerInvariant());

            Assert.Equal(BigInteger.Parse("850000000000000000000"), BigInteger.Parse(addr1Balance.Balance!));
            Assert.Equal(BigInteger.Parse("100000000000000000000"), BigInteger.Parse(addr2Balance.Balance!));
            Assert.Equal(BigInteger.Parse("50000000000000000000"), BigInteger.Parse(addr3Balance.Balance!));
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC20Mint_When_Processed_Then_MintFromZeroAddressNotDebited()
        {
            var (_, balanceRepo, _) = await RunPipeline(new[] { _fixture.ERC20Address });

            var zeroAddrBalances = (await balanceRepo.GetByAddressAsync("0x0000000000000000000000000000000000000000")).ToList();
            Assert.Empty(zeroAddrBalances);
        }

        [Fact]
        [Trait("Category", "TokenPipeline-Integration")]
        public async Task Given_ERC20TransferLogs_When_Queried_Then_FieldsMatchOnChain()
        {
            var (transferLogRepo, _, _) = await RunPipeline(new[] { _fixture.ERC20Address });

            var mintLog = transferLogRepo.Records
                .First(r => r.TransactionHash == _fixture.ERC20MintReceipt.TransactionHash);

            Assert.Equal(_fixture.ERC20MintReceipt.TransactionHash, mintLog.TransactionHash);
            Assert.Equal((long)_fixture.ERC20MintReceipt.BlockNumber.Value, mintLog.BlockNumber);
            Assert.Equal(_fixture.ERC20Address.ToLowerInvariant(), mintLog.ContractAddress?.ToLowerInvariant());
            Assert.NotNull(mintLog.LogIndex);
        }
    }
}
