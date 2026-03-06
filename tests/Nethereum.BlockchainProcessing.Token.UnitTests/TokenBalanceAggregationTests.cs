using System.Numerics;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class TokenBalanceAggregationTests
    {
        private readonly InMemoryTokenTransferLogRepository _transferLogRepo;
        private readonly InMemoryTokenBalanceRepository _balanceRepo;
        private readonly InMemoryNFTInventoryRepository _nftRepo;
        private readonly InMemoryBlockchainProgressRepository _progressRepo;
        private readonly TokenBalanceAggregationService _service;

        public TokenBalanceAggregationTests()
        {
            _transferLogRepo = new InMemoryTokenTransferLogRepository();
            _balanceRepo = new InMemoryTokenBalanceRepository();
            _nftRepo = new InMemoryNFTInventoryRepository();
            _progressRepo = new InMemoryBlockchainProgressRepository();
            _service = new TokenBalanceAggregationService(
                _transferLogRepo, _balanceRepo, _nftRepo, _progressRepo);
        }

        [Fact]
        public async Task ProcessTransferAsync_ERC20_UpdatesSenderAndRecipientBalances()
        {
            var transfer = new TokenTransferLog
            {
                FromAddress = "0x1111111111111111111111111111111111111111",
                ToAddress = "0x2222222222222222222222222222222222222222",
                ContractAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Amount = "1000",
                TokenType = "ERC20",
                BlockNumber = 1,
                IsCanonical = true
            };

            await _service.ProcessTransferAsync(transfer);

            var senderBalances = await _balanceRepo.GetByAddressAsync(transfer.FromAddress);
            var recipientBalances = await _balanceRepo.GetByAddressAsync(transfer.ToAddress);

            var senderBalance = senderBalances.First();
            var recipientBalance = recipientBalances.First();

            Assert.Equal("0", senderBalance.Balance);
            Assert.Equal("1000", recipientBalance.Balance);
        }

        [Fact]
        public async Task ProcessTransferAsync_ERC20_AccumulatesMultipleTransfers()
        {
            var contract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var sender = "0x1111111111111111111111111111111111111111";
            var recipient = "0x2222222222222222222222222222222222222222";

            await _service.ProcessTransferAsync(new TokenTransferLog
            {
                FromAddress = "0x0000000000000000000000000000000000000000",
                ToAddress = sender,
                ContractAddress = contract,
                Amount = "5000",
                TokenType = "ERC20",
                BlockNumber = 1,
                IsCanonical = true
            });

            await _service.ProcessTransferAsync(new TokenTransferLog
            {
                FromAddress = sender,
                ToAddress = recipient,
                ContractAddress = contract,
                Amount = "2000",
                TokenType = "ERC20",
                BlockNumber = 2,
                IsCanonical = true
            });

            var senderBalances = await _balanceRepo.GetByAddressAsync(sender);
            var recipientBalances = await _balanceRepo.GetByAddressAsync(recipient);

            Assert.Equal("3000", senderBalances.First().Balance);
            Assert.Equal("2000", recipientBalances.First().Balance);
        }

        [Fact]
        public async Task ProcessTransferAsync_ERC20_MintFromZeroAddress_SkipsSenderDebit()
        {
            var recipient = "0x2222222222222222222222222222222222222222";
            var contract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            await _service.ProcessTransferAsync(new TokenTransferLog
            {
                FromAddress = "0x0000000000000000000000000000000000000000",
                ToAddress = recipient,
                ContractAddress = contract,
                Amount = "500",
                TokenType = "ERC20",
                BlockNumber = 1,
                IsCanonical = true
            });

            var zeroBalances = await _balanceRepo.GetByAddressAsync("0x0000000000000000000000000000000000000000");
            var recipientBalances = await _balanceRepo.GetByAddressAsync(recipient);

            Assert.Empty(zeroBalances);
            Assert.Equal("500", recipientBalances.First().Balance);
        }

        [Fact]
        public async Task ProcessTransferAsync_ERC721_TracksOwnershipInNFTInventory()
        {
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var tokenId = "42";

            await _nftRepo.UpsertAsync(new NFTInventory
            {
                Address = from,
                ContractAddress = contract,
                TokenId = tokenId,
                Amount = "1",
                TokenType = "ERC721",
                LastUpdatedBlockNumber = 0
            });

            await _service.ProcessTransferAsync(new TokenTransferLog
            {
                FromAddress = from,
                ToAddress = to,
                ContractAddress = contract,
                TokenId = tokenId,
                TokenType = "ERC721",
                BlockNumber = 1,
                IsCanonical = true
            });

            var nft = await _nftRepo.GetByTokenAsync(contract, tokenId);
            Assert.Equal(to, nft.Address);
            Assert.Equal("1", nft.Amount);
        }

        [Fact]
        public async Task ProcessTransferAsync_ERC1155_UpdatesAmountsCorrectly()
        {
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var contract = "0xcccccccccccccccccccccccccccccccccccccccc";
            var tokenId = "7";

            await _nftRepo.UpsertAsync(new NFTInventory
            {
                Address = from,
                ContractAddress = contract,
                TokenId = tokenId,
                Amount = "100",
                TokenType = "ERC1155",
                LastUpdatedBlockNumber = 0
            });

            await _service.ProcessTransferAsync(new TokenTransferLog
            {
                FromAddress = from,
                ToAddress = to,
                ContractAddress = contract,
                TokenId = tokenId,
                Amount = "30",
                TokenType = "ERC1155",
                BlockNumber = 1,
                IsCanonical = true
            });

            var fromNft = await _nftRepo.GetByTokenAsync(contract, tokenId);
            Assert.Equal("70", fromNft.Amount);
        }

        [Fact]
        public async Task ProcessTransferLogsAsync_SkipsNonCanonicalLogs()
        {
            var logs = new List<ITokenTransferLogView>
            {
                new TokenTransferLog
                {
                    FromAddress = "0x0000000000000000000000000000000000000000",
                    ToAddress = "0x2222222222222222222222222222222222222222",
                    ContractAddress = "0xaaaa",
                    Amount = "100",
                    TokenType = "ERC20",
                    BlockNumber = 1,
                    IsCanonical = true
                },
                new TokenTransferLog
                {
                    FromAddress = "0x0000000000000000000000000000000000000000",
                    ToAddress = "0x2222222222222222222222222222222222222222",
                    ContractAddress = "0xaaaa",
                    Amount = "200",
                    TokenType = "ERC20",
                    BlockNumber = 1,
                    IsCanonical = false
                }
            };

            await _service.ProcessTransferLogsAsync(logs);

            var balances = await _balanceRepo.GetByAddressAsync("0x2222222222222222222222222222222222222222");
            Assert.Single(balances);
            Assert.Equal("100", balances.First().Balance);
        }

        [Fact]
        public async Task ProcessFromCheckpointAsync_ProcessesLogsSequentially()
        {
            await _transferLogRepo.UpsertAsync(new TokenTransferLog
            {
                TransactionHash = "0xtx1",
                LogIndex = 0,
                FromAddress = "0x0000000000000000000000000000000000000000",
                ToAddress = "0x2222222222222222222222222222222222222222",
                ContractAddress = "0xaaaa",
                Amount = "500",
                TokenType = "ERC20",
                BlockNumber = 0,
                IsCanonical = true
            });

            await _service.ProcessFromCheckpointAsync();

            var balances = await _balanceRepo.GetByAddressAsync("0x2222222222222222222222222222222222222222");
            Assert.Single(balances);
            Assert.Equal("500", balances.First().Balance);
        }
    }
}
