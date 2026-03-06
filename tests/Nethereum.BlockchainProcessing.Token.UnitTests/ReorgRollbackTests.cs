using System.Numerics;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class ReorgRollbackTests
    {
        private const string Addr1 = "0x1111111111111111111111111111111111111111";
        private const string Addr2 = "0x2222222222222222222222222222222222222222";
        private const string Addr3 = "0x3333333333333333333333333333333333333333";
        private const string Zero = "0x0000000000000000000000000000000000000000";
        private const string ERC20Contract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ERC721Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string ERC1155Contract = "0xcccccccccccccccccccccccccccccccccccccccc";

        private readonly InMemoryTokenTransferLogRepository _transferLogRepo;
        private readonly InMemoryTokenBalanceRepository _balanceRepo;
        private readonly InMemoryNFTInventoryRepository _nftRepo;
        private readonly InMemoryBlockchainProgressRepository _progressRepo;
        private readonly TokenBalanceAggregationService _service;

        public ReorgRollbackTests()
        {
            _transferLogRepo = new InMemoryTokenTransferLogRepository();
            _balanceRepo = new InMemoryTokenBalanceRepository();
            _nftRepo = new InMemoryNFTInventoryRepository();
            _progressRepo = new InMemoryBlockchainProgressRepository();
            _service = new TokenBalanceAggregationService(
                _transferLogRepo, _balanceRepo, _nftRepo, _progressRepo);
        }

        private async Task SimulateReorgRollback(BigInteger fromBlock, BigInteger toBlock)
        {
            for (var block = fromBlock; block <= toBlock; block++)
            {
                await _transferLogRepo.MarkNonCanonicalAsync(block);
                await _balanceRepo.DeleteByBlockNumberAsync(block);
                await _nftRepo.DeleteByBlockNumberAsync(block);
            }
        }

        [Fact]
        public async Task Given_ERC20BalancesFromReorgedBlocks_When_RollbackAndReaggregate_Then_BalancesMatchNewChain()
        {
            // GIVEN: Mint 1000 to Addr1 at block 5, transfer 400 to Addr2 at block 6 (old chain)
            var mintLog = CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 5);
            var transferLog = CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "400", null, "ERC20", 6);

            await _transferLogRepo.UpsertAsync(mintLog);
            await _transferLogRepo.UpsertAsync(transferLog);
            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            Assert.Equal("600", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("400", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);

            // WHEN: Reorg at block 6 — old transfer replaced with 200 instead of 400
            await SimulateReorgRollback(6, 6);

            var newTransferLog = CreateTransferLog("0xtx3", 0, Addr1, Addr2, ERC20Contract, "200", null, "ERC20", 6);
            await _transferLogRepo.UpsertAsync(newTransferLog);

            // Clear balance repo and re-aggregate from all canonical logs
            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Balances reflect new chain (1000-200=800 for Addr1, 200 for Addr2)
            Assert.Equal("800", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("200", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);
        }

        [Fact]
        public async Task Given_ERC721OwnershipFromReorgedBlock_When_RollbackAndReaggregate_Then_OwnershipRestored()
        {
            // GIVEN: Mint NFT #1 to Addr1 at block 5, transfer to Addr2 at block 6
            var mintLog = CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC721Contract, null, "1", "ERC721", 5);
            var transferLog = CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC721Contract, null, "1", "ERC721", 6);

            await _transferLogRepo.UpsertAsync(mintLog);
            await _transferLogRepo.UpsertAsync(transferLog);
            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            var nft = await _nftRepo.GetByTokenAsync(ERC721Contract, "1");
            Assert.Equal(Addr2, nft.Address);

            // WHEN: Reorg at block 6 — transfer never happened in new chain
            await SimulateReorgRollback(6, 6);

            _balanceRepo.Records.Clear();
            _nftRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Addr1 still owns NFT #1
            nft = await _nftRepo.GetByTokenAsync(ERC721Contract, "1");
            Assert.Equal(Addr1, nft.Address);
            Assert.Equal("1", nft.Amount);

            var addr2Nfts = await _nftRepo.GetByAddressAsync(Addr2);
            Assert.Empty(addr2Nfts);
        }

        [Fact]
        public async Task Given_ERC1155AmountsFromReorgedBlock_When_RollbackAndReaggregate_Then_AmountsRestored()
        {
            // GIVEN: Mint 100 of tokenId=1 to Addr1 at block 5, transfer 30 to Addr2 at block 6
            var mintLog = CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC1155Contract, "100", "1", "ERC1155", 5);
            var transferLog = CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC1155Contract, "30", "1", "ERC1155", 6);

            await _transferLogRepo.UpsertAsync(mintLog);
            await _transferLogRepo.UpsertAsync(transferLog);
            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            Assert.Equal("70", (await _nftRepo.GetByTokenAsync(ERC1155Contract, "1")).Amount);

            // WHEN: Reorg at block 6 — transfer was 50 instead of 30
            await SimulateReorgRollback(6, 6);

            var newTransfer = CreateTransferLog("0xtx3", 0, Addr1, Addr2, ERC1155Contract, "50", "1", "ERC1155", 6);
            await _transferLogRepo.UpsertAsync(newTransfer);

            _nftRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Addr1 has 50 (100-50), Addr2 has 50
            var addr1Nft = _nftRepo.Records.First(n => n.Address == Addr1 && n.TokenId == "1");
            var addr2Nft = _nftRepo.Records.First(n => n.Address == Addr2 && n.TokenId == "1");
            Assert.Equal("50", addr1Nft.Amount);
            Assert.Equal("50", addr2Nft.Amount);
        }

        [Fact]
        public async Task Given_MixedTokenTypesInReorgedRange_When_RollbackAndReaggregate_Then_AllTypesConsistent()
        {
            // GIVEN: Block 5: ERC20 mint, Block 6: ERC721 mint, Block 7: ERC1155 mint + ERC20 transfer
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 5));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Zero, Addr1, ERC721Contract, null, "1", "ERC721", 6));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Zero, Addr1, ERC1155Contract, "200", "10", "ERC1155", 7));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx4", 1, Addr1, Addr2, ERC20Contract, "300", null, "ERC20", 7));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            // WHEN: Reorg blocks 6-7
            await SimulateReorgRollback(6, 7);

            // New chain: block 6 has ERC20 transfer of 500 (not 300), no ERC721 mint, no ERC1155 mint
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx5", 0, Addr1, Addr3, ERC20Contract, "500", null, "ERC20", 6));

            _balanceRepo.Records.Clear();
            _nftRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: ERC20 — Addr1=500, Addr3=500 (mint 1000 - transfer 500)
            Assert.Equal("500", (await _balanceRepo.GetByAddressAsync(Addr1)).First(b => b.ContractAddress == ERC20Contract).Balance);
            Assert.Equal("500", (await _balanceRepo.GetByAddressAsync(Addr3)).First().Balance);

            // ERC721 — no NFTs (mint was in reorged block 6)
            Assert.Empty(await _nftRepo.GetByAddressAsync(Addr1));

            // ERC1155 — no inventory (mint was in reorged block 7)
            Assert.Empty(_nftRepo.Records.Where(n => n.ContractAddress == ERC1155Contract));

            // Addr2 has no balance (old transfer was in reorged block 7)
            Assert.Empty(await _balanceRepo.GetByAddressAsync(Addr2));
        }

        [Fact]
        public async Task Given_CanonicalAndNonCanonicalLogs_When_ProcessTransferLogsAsync_Then_OnlyCanonicalProcessed()
        {
            var canonical = CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "500", null, "ERC20", 5);
            var nonCanonical = CreateTransferLog("0xtx2", 0, Zero, Addr1, ERC20Contract, "999", null, "ERC20", 5);
            nonCanonical.IsCanonical = false;

            var logs = new List<ITokenTransferLogView> { canonical, nonCanonical };
            await _service.ProcessTransferLogsAsync(logs);

            var balances = await _balanceRepo.GetByAddressAsync(Addr1);
            Assert.Single(balances);
            Assert.Equal("500", balances.First().Balance);
        }

        [Fact]
        public async Task Given_NonCanonicalLogsInRepo_When_ProcessFromCheckpointAsync_Then_SkipsNonCanonical()
        {
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "100", null, "ERC20", 0));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Zero, Addr1, ERC20Contract, "200", null, "ERC20", 1));

            // Mark block 1 as non-canonical
            await _transferLogRepo.MarkNonCanonicalAsync(1);

            await _service.ProcessFromCheckpointAsync();

            var balances = await _balanceRepo.GetByAddressAsync(Addr1);
            Assert.Single(balances);
            Assert.Equal("100", balances.First().Balance);
        }

        [Fact]
        public async Task Given_FullPipeline_When_ReorgOccurs_Then_RollbackAndReprocessYieldsConsistentState()
        {
            // Phase 1: Process blocks 0-3 (old chain)
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 0));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 1));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Addr1, Addr3, ERC20Contract, "200", null, "ERC20", 2));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx4", 0, Addr2, Addr3, ERC20Contract, "50", null, "ERC20", 3));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());
            await _progressRepo.UpsertProgressAsync(3);

            // Verify old chain state: Addr1=700, Addr2=50, Addr3=250
            Assert.Equal("700", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("50", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);
            Assert.Equal("250", (await _balanceRepo.GetByAddressAsync(Addr3)).First().Balance);

            // Phase 2: Reorg at blocks 2-3
            await SimulateReorgRollback(2, 3);
            await _progressRepo.UpsertProgressAsync(1); // Rewind progress to block 1

            // New chain: block 2 has different transfer (Addr1->Addr2 of 300), block 3 nothing
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx5", 0, Addr1, Addr2, ERC20Contract, "300", null, "ERC20", 2));

            // Phase 3: Re-aggregate from all canonical logs
            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Addr1=600 (1000-100-300), Addr2=400 (100+300), Addr3=0 (no transfers in new chain)
            Assert.Equal("600", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("400", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);
            Assert.Empty(await _balanceRepo.GetByAddressAsync(Addr3));
        }

        [Fact]
        public async Task Given_ReorgAtGenesis_When_Rollback_Then_AllDataCleared()
        {
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "500", null, "ERC20", 0));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Zero, Addr1, ERC721Contract, null, "1", "ERC721", 1));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());
            Assert.NotEmpty(_balanceRepo.Records);

            // Reorg everything from block 0
            await SimulateReorgRollback(0, 1);
            _balanceRepo.Records.Clear();
            _nftRepo.Records.Clear();

            // Re-aggregate — no canonical logs remain
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            Assert.Empty(_balanceRepo.Records);
            Assert.Empty(_nftRepo.Records);
        }

        [Fact]
        public async Task Given_TwoSequentialReorgs_When_RollbackEachTime_Then_FinalStateConsistent()
        {
            // Initial: mint 1000 at block 0
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 0));

            // Block 1 (old): transfer 100 to Addr2
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 1));
            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            Assert.Equal("900", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);

            // Reorg #1: block 1 now has transfer 200
            await SimulateReorgRollback(1, 1);
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Addr1, Addr2, ERC20Contract, "200", null, "ERC20", 1));

            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            Assert.Equal("800", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);

            // Block 2: transfer 50 to Addr3
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx4", 0, Addr1, Addr3, ERC20Contract, "50", null, "ERC20", 2));
            await _service.ProcessTransferAsync(_transferLogRepo.Records.Last());

            // Reorg #2: blocks 1-2 now have completely different transfers
            await SimulateReorgRollback(1, 2);
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx5", 0, Addr1, Addr3, ERC20Contract, "600", null, "ERC20", 1));

            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Only mint(1000) + transfer(600 to Addr3) remain canonical
            Assert.Equal("400", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("600", (await _balanceRepo.GetByAddressAsync(Addr3)).First().Balance);
            Assert.Empty(await _balanceRepo.GetByAddressAsync(Addr2));
        }

        [Fact]
        public async Task Given_PartialReorg_When_OnlySomeBlocksAffected_Then_UnaffectedBlocksPreserved()
        {
            // Blocks 0-4 all have ERC20 transfers
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx0", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 0));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 1));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 2));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Addr1, Addr3, ERC20Contract, "100", null, "ERC20", 3));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx4", 0, Addr1, Addr3, ERC20Contract, "100", null, "ERC20", 4));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            // Reorg only blocks 3-4 (blocks 0-2 unaffected)
            await SimulateReorgRollback(3, 4);

            // New chain: block 3 sends 50 to Addr2 (not Addr3)
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx5", 0, Addr1, Addr2, ERC20Contract, "50", null, "ERC20", 3));

            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // THEN: Blocks 0-2 canonical: mint(1000), tx1(100 to Addr2), tx2(100 to Addr2)
            // Block 3 new: tx5(50 to Addr2). Blocks 4: nothing
            // Addr1 = 1000 - 100 - 100 - 50 = 750
            // Addr2 = 100 + 100 + 50 = 250
            // Addr3 = 0 (reorged transfers gone)
            Assert.Equal("750", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("250", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);
            Assert.Empty(await _balanceRepo.GetByAddressAsync(Addr3));
        }

        [Fact]
        public async Task Given_MarkNonCanonical_When_QueryingTransferLogs_Then_ExcludedFromResults()
        {
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 5));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "200", null, "ERC20", 6));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Addr1, Addr2, ERC20Contract, "300", null, "ERC20", 7));

            await _transferLogRepo.MarkNonCanonicalAsync(6);

            var addr1Logs = await _transferLogRepo.GetByAddressAsync(Addr1, 1, 100);
            var addr2Logs = await _transferLogRepo.GetByAddressAsync(Addr2, 1, 100);
            var block6Logs = await _transferLogRepo.GetByBlockNumberAsync(6);

            Assert.Equal(2, addr1Logs.Count());
            Assert.Equal(2, addr2Logs.Count());
            Assert.Empty(block6Logs);

            // Underlying records still exist (3 total), but queries filter
            Assert.Equal(3, _transferLogRepo.Records.Count);
            Assert.False(_transferLogRepo.Records.Single(r => r.BlockNumber == 6).IsCanonical);
        }

        [Fact]
        public async Task Given_ProcessedBlocks_When_RollbackAggregationAsync_Then_MarksLogsAndDeletesDerivedData()
        {
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "1000", null, "ERC20", 5));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "300", null, "ERC20", 6));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Zero, Addr1, ERC721Contract, null, "1", "ERC721", 6));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            Assert.Equal("700", (await _balanceRepo.GetByAddressAsync(Addr1)).First(b => b.ContractAddress == ERC20Contract).Balance);
            Assert.NotEmpty(_nftRepo.Records);

            // WHEN: RollbackAggregationAsync for block 6
            await _service.RollbackAggregationAsync(6, 6);

            // THEN: Block 6 logs marked non-canonical
            Assert.False(_transferLogRepo.Records.Single(r => r.BlockNumber == 6 && r.TokenType == "ERC20").IsCanonical);
            Assert.False(_transferLogRepo.Records.Single(r => r.BlockNumber == 6 && r.TokenType == "ERC721").IsCanonical);
            Assert.True(_transferLogRepo.Records.Single(r => r.BlockNumber == 5).IsCanonical);

            // THEN: Derived data for block 6 deleted (balances updated at block 6)
            var addr2Balances = await _balanceRepo.GetByAddressAsync(Addr2);
            Assert.Empty(addr2Balances);
        }

        [Fact]
        public async Task Given_RollbackAggregation_When_ReaggregateFromCanonical_Then_StateConsistent()
        {
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "500", null, "ERC20", 0));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx2", 0, Addr1, Addr2, ERC20Contract, "100", null, "ERC20", 1));
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx3", 0, Addr1, Addr3, ERC20Contract, "200", null, "ERC20", 2));

            await _service.ProcessTransferLogsAsync(_transferLogRepo.Records.Cast<ITokenTransferLogView>().ToList());

            // Rollback block 2 using new API
            await _service.RollbackAggregationAsync(2, 2);

            // New canonical log for block 2: Addr1 → Addr2 of 50
            await _transferLogRepo.UpsertAsync(CreateTransferLog("0xtx4", 0, Addr1, Addr2, ERC20Contract, "50", null, "ERC20", 2));

            // Re-aggregate from all canonical logs
            _balanceRepo.Records.Clear();
            await _service.ProcessTransferLogsAsync(
                _transferLogRepo.Records.Where(r => r.IsCanonical).Cast<ITokenTransferLogView>().ToList());

            // Addr1 = 500 - 100 - 50 = 350, Addr2 = 100 + 50 = 150, Addr3 = 0
            Assert.Equal("350", (await _balanceRepo.GetByAddressAsync(Addr1)).First().Balance);
            Assert.Equal("150", (await _balanceRepo.GetByAddressAsync(Addr2)).First().Balance);
            Assert.Empty(await _balanceRepo.GetByAddressAsync(Addr3));
        }

        [Fact]
        public async Task Given_TransferLogRepoWithoutNonCanonical_When_RollbackAggregation_Then_StillDeletesDerived()
        {
            // Use a repo that only implements ITokenTransferLogRepository (not INonCanonicalTokenTransferLogRepository)
            var plainRepo = new NonCanonicalUnawareTransferLogRepo();
            var service = new TokenBalanceAggregationService(
                plainRepo, _balanceRepo, _nftRepo, _progressRepo);

            plainRepo.Records.Add(CreateTransferLog("0xtx1", 0, Zero, Addr1, ERC20Contract, "500", null, "ERC20", 5));

            await service.ProcessTransferLogsAsync(plainRepo.Records.Cast<ITokenTransferLogView>().ToList());
            Assert.NotEmpty(_balanceRepo.Records);

            // RollbackAggregation should still delete derived data even though repo doesn't support MarkNonCanonical
            await service.RollbackAggregationAsync(5, 5);

            Assert.Empty(_balanceRepo.Records);
            // The logs remain (no IsCanonical marking possible)
            Assert.Single(plainRepo.Records);
        }

        private static TokenTransferLog CreateTransferLog(
            string txHash, long logIndex, string from, string to,
            string contract, string? amount, string? tokenId,
            string tokenType, long blockNumber)
        {
            return new TokenTransferLog
            {
                TransactionHash = txHash,
                LogIndex = logIndex,
                FromAddress = from,
                ToAddress = to,
                ContractAddress = contract,
                Amount = amount,
                TokenId = tokenId,
                TokenType = tokenType,
                BlockNumber = blockNumber,
                BlockHash = $"0xhash_{blockNumber}",
                IsCanonical = true
            };
        }

        private class NonCanonicalUnawareTransferLogRepo : ITokenTransferLogRepository
        {
            public List<TokenTransferLog> Records { get; } = new List<TokenTransferLog>();

            public Task UpsertAsync(TokenTransferLog log)
            {
                Records.Add(log);
                return Task.CompletedTask;
            }

            public Task<ITokenTransferLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
                => Task.FromResult<ITokenTransferLogView>(null);

            public Task<IEnumerable<ITokenTransferLogView>> GetByAddressAsync(string address, int page, int pageSize)
                => Task.FromResult(Records.Cast<ITokenTransferLogView>());

            public Task<IEnumerable<ITokenTransferLogView>> GetByContractAsync(string contractAddress, int page, int pageSize)
                => Task.FromResult(Records.Cast<ITokenTransferLogView>());

            public Task<IEnumerable<ITokenTransferLogView>> GetByBlockNumberAsync(long blockNumber)
                => Task.FromResult(Records.Where(r => r.BlockNumber == blockNumber).Cast<ITokenTransferLogView>());
        }
    }
}
