using System.Numerics;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class InMemoryTokenTransferLogRepositoryTests
    {
        private readonly InMemoryTokenTransferLogRepository _repo = new();

        [Fact]
        public async Task UpsertAsync_AddsNewRecord()
        {
            var log = CreateLog("0xtx1", 0, "0xfrom", "0xto", "0xcontract", "100");
            await _repo.UpsertAsync(log);

            Assert.Single(_repo.Records);
        }

        [Fact]
        public async Task UpsertAsync_UpdatesExistingRecord()
        {
            var log1 = CreateLog("0xtx1", 0, "0xfrom", "0xto", "0xcontract", "100");
            await _repo.UpsertAsync(log1);

            var log2 = CreateLog("0xtx1", 0, "0xfrom", "0xto2", "0xcontract", "200");
            await _repo.UpsertAsync(log2);

            Assert.Single(_repo.Records);
            Assert.Equal("0xto2", _repo.Records[0].ToAddress);
            Assert.Equal("200", _repo.Records[0].Amount);
        }

        [Fact]
        public async Task FindByTransactionHashAndLogIndexAsync_FindsRecord()
        {
            var log = CreateLog("0xtx1", 5, "0xfrom", "0xto", "0xcontract", "100");
            await _repo.UpsertAsync(log);

            var found = await _repo.FindByTransactionHashAndLogIndexAsync("0xtx1", 5);
            Assert.NotNull(found);
            Assert.Equal("0xfrom", found.FromAddress);
        }

        [Fact]
        public async Task GetByAddressAsync_ReturnsCanonicalOnly()
        {
            await _repo.UpsertAsync(CreateLog("0xtx1", 0, "0xfrom", "0xto", "0xcontract", "100", canonical: true));
            await _repo.UpsertAsync(CreateLog("0xtx2", 0, "0xfrom", "0xto2", "0xcontract", "200", canonical: false));

            var results = await _repo.GetByAddressAsync("0xfrom", 1, 10);
            Assert.Single(results);
        }

        [Fact]
        public async Task GetByAddressAsync_MatchesBothFromAndTo()
        {
            await _repo.UpsertAsync(CreateLog("0xtx1", 0, "0xfrom", "0xaddr", "0xcontract", "100"));
            await _repo.UpsertAsync(CreateLog("0xtx2", 0, "0xaddr", "0xto", "0xcontract", "200"));

            var results = await _repo.GetByAddressAsync("0xaddr", 1, 10);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public async Task GetByContractAsync_FiltersByContract()
        {
            await _repo.UpsertAsync(CreateLog("0xtx1", 0, "0xfrom", "0xto", "0xcontract1", "100"));
            await _repo.UpsertAsync(CreateLog("0xtx2", 0, "0xfrom", "0xto", "0xcontract2", "200"));

            var results = await _repo.GetByContractAsync("0xcontract1", 1, 10);
            Assert.Single(results);
        }

        [Fact]
        public async Task MarkNonCanonicalAsync_MarksLogsForBlock()
        {
            await _repo.UpsertAsync(CreateLog("0xtx1", 0, "0xfrom", "0xto", "0xcontract", "100", blockNumber: 50));
            await _repo.UpsertAsync(CreateLog("0xtx2", 0, "0xfrom", "0xto", "0xcontract", "200", blockNumber: 51));

            await _repo.MarkNonCanonicalAsync(50);

            Assert.False(_repo.Records[0].IsCanonical);
            Assert.True(_repo.Records[1].IsCanonical);
        }

        [Fact]
        public async Task GetByAddressAsync_PaginatesCorrectly()
        {
            for (int i = 0; i < 5; i++)
            {
                await _repo.UpsertAsync(CreateLog($"0xtx{i}", 0, "0xaddr", "0xto", "0xcontract", $"{i * 100}", blockNumber: i));
            }

            var page1 = await _repo.GetByAddressAsync("0xaddr", 1, 2);
            var page2 = await _repo.GetByAddressAsync("0xaddr", 2, 2);

            Assert.Equal(2, page1.Count());
            Assert.Equal(2, page2.Count());
        }

        private static TokenTransferLog CreateLog(string txHash, long logIndex,
            string from, string to, string contract, string amount,
            bool canonical = true, long blockNumber = 1)
        {
            return new TokenTransferLog
            {
                TransactionHash = txHash,
                LogIndex = logIndex,
                FromAddress = from,
                ToAddress = to,
                ContractAddress = contract,
                Amount = amount,
                TokenType = "ERC20",
                BlockNumber = blockNumber,
                BlockHash = "0xblockhash",
                IsCanonical = canonical
            };
        }
    }

    public class InMemoryTokenBalanceRepositoryTests
    {
        private readonly InMemoryTokenBalanceRepository _repo = new();

        [Fact]
        public async Task UpsertAsync_AddsNewBalance()
        {
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr",
                ContractAddress = "0xcontract",
                Balance = "1000",
                TokenType = "ERC20",
                LastUpdatedBlockNumber = 1
            });

            Assert.Single(_repo.Records);
        }

        [Fact]
        public async Task UpsertAsync_UpdatesExisting_CaseInsensitive()
        {
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xAddr",
                ContractAddress = "0xContract",
                Balance = "1000",
                TokenType = "ERC20",
                LastUpdatedBlockNumber = 1
            });

            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr",
                ContractAddress = "0xcontract",
                Balance = "2000",
                TokenType = "ERC20",
                LastUpdatedBlockNumber = 2
            });

            Assert.Single(_repo.Records);
            Assert.Equal("2000", _repo.Records[0].Balance);
        }

        [Fact]
        public async Task GetByAddressAsync_ReturnsByAddress()
        {
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr",
                ContractAddress = "0xc1",
                Balance = "100",
                TokenType = "ERC20"
            });
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr",
                ContractAddress = "0xc2",
                Balance = "200",
                TokenType = "ERC20"
            });
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xother",
                ContractAddress = "0xc1",
                Balance = "300",
                TokenType = "ERC20"
            });

            var results = await _repo.GetByAddressAsync("0xaddr");
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public async Task DeleteByBlockNumberAsync_DeletesMatchingRecords()
        {
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr",
                ContractAddress = "0xc1",
                Balance = "100",
                LastUpdatedBlockNumber = 5
            });
            await _repo.UpsertAsync(new TokenBalance
            {
                Address = "0xaddr2",
                ContractAddress = "0xc1",
                Balance = "200",
                LastUpdatedBlockNumber = 6
            });

            await _repo.DeleteByBlockNumberAsync(5);

            Assert.Single(_repo.Records);
            Assert.Equal("0xaddr2", _repo.Records[0].Address);
        }
    }

    public class InMemoryNFTInventoryRepositoryTests
    {
        private readonly InMemoryNFTInventoryRepository _repo = new();

        [Fact]
        public async Task UpsertAsync_AddsNewItem()
        {
            await _repo.UpsertAsync(new NFTInventory
            {
                Address = "0xowner",
                ContractAddress = "0xnft",
                TokenId = "1",
                Amount = "1",
                TokenType = "ERC721"
            });

            Assert.Single(_repo.Records);
        }

        [Fact]
        public async Task GetByTokenAsync_FindsByContractAndTokenId()
        {
            await _repo.UpsertAsync(new NFTInventory
            {
                Address = "0xowner",
                ContractAddress = "0xnft",
                TokenId = "42",
                Amount = "1",
                TokenType = "ERC721"
            });

            var result = await _repo.GetByTokenAsync("0xnft", "42");
            Assert.NotNull(result);
            Assert.Equal("0xowner", result.Address);
        }

        [Fact]
        public async Task GetByTokenAsync_ReturnsNullWhenNotFound()
        {
            var result = await _repo.GetByTokenAsync("0xnft", "999");
            Assert.Null(result);
        }

        [Fact]
        public async Task UpsertAsync_UpdatesExisting_ByAddressContractTokenId()
        {
            await _repo.UpsertAsync(new NFTInventory
            {
                Address = "0xowner",
                ContractAddress = "0xnft",
                TokenId = "1",
                Amount = "10",
                TokenType = "ERC1155"
            });

            await _repo.UpsertAsync(new NFTInventory
            {
                Address = "0xowner",
                ContractAddress = "0xnft",
                TokenId = "1",
                Amount = "20",
                TokenType = "ERC1155"
            });

            Assert.Single(_repo.Records);
            Assert.Equal("20", _repo.Records[0].Amount);
        }
    }
}
