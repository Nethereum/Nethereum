using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AppChain.Anchoring.Postgres.Entities;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;
using Nethereum.Documentation;
using Nethereum.Explorer.Anchoring;
using Nethereum.Explorer.Anchoring.Services;
using Nethereum.Explorer.Services;
using Nethereum.Explorer.Services.Localization;
using Xunit;

namespace Nethereum.Explorer.UnitTests
{
    public class AnchorExplorerServiceTests
    {
        private sealed class InMemoryAnchors : IAnchorRecordRepository
        {
            public List<AnchorRecord> Records { get; } = new();

            public Task UpsertAsync(AnchorRecord record)
            {
                Records.Add(record);
                return Task.CompletedTask;
            }

            public Task<AnchorRecord> GetLatestAsync(long chainId) =>
                Task.FromResult(Records.Where(r => r.ChainId == chainId).OrderByDescending(r => r.EndBlock).FirstOrDefault()!);

            public Task<AnchorRecord> GetByEndBlockAsync(long chainId, long endBlock) =>
                Task.FromResult(Records.FirstOrDefault(r => r.ChainId == chainId && r.EndBlock == endBlock)!);

            public Task<IReadOnlyList<AnchorRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50) =>
                Task.FromResult<IReadOnlyList<AnchorRecord>>(
                    Records.Where(r => r.ChainId == chainId).OrderBy(r => r.EndBlock).Skip(skip).Take(take).ToList());

            public Task<int> GetCountByChainAsync(long chainId) =>
                Task.FromResult(Records.Count(r => r.ChainId == chainId));
        }

        private sealed class InMemoryRegistrations : IChainRegistrationRepository
        {
            public List<ChainRegistration> Records { get; } = new();

            public Task UpsertAsync(ChainRegistration registration)
            {
                Records.Add(registration);
                return Task.CompletedTask;
            }

            public Task<ChainRegistration?> GetByChainIdAsync(long chainId) =>
                Task.FromResult(Records.FirstOrDefault(r => r.ChainId == chainId));

            public Task<IReadOnlyList<ChainRegistration>> GetAllAsync() =>
                Task.FromResult<IReadOnlyList<ChainRegistration>>(Records.ToList());
        }

        private sealed class InMemoryProofs : IBlockProofRecordRepository
        {
            public List<BlockProofRecord> Records { get; } = new();

            public Task UpsertAsync(BlockProofRecord record)
            {
                Records.Add(record);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<BlockProofRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50) =>
                Task.FromResult<IReadOnlyList<BlockProofRecord>>(
                    Records.Where(r => r.ChainId == chainId).OrderBy(r => r.BlockNumber).Skip(skip).Take(take).ToList());

            public Task<bool> IsBlockProvenAsync(long chainId, long blockNumber) =>
                Task.FromResult(Records.Any(r => r.ChainId == chainId && r.BlockNumber == blockNumber));
        }

        private sealed class InMemorySummaries : IChainAnchoringSummaryRepository
        {
            public List<ChainAnchoringSummary> Records { get; } = new();

            public Task UpsertAsync(ChainAnchoringSummary summary)
            {
                Records.RemoveAll(r => r.ChainId == summary.ChainId);
                Records.Add(summary);
                return Task.CompletedTask;
            }

            public Task<ChainAnchoringSummary?> GetAsync(long chainId) =>
                Task.FromResult(Records.FirstOrDefault(r => r.ChainId == chainId));

            public Task<IReadOnlyList<ChainAnchoringSummary>> GetAllAsync() =>
                Task.FromResult<IReadOnlyList<ChainAnchoringSummary>>(Records.ToList());
        }

        [Fact]
        [NethereumDocExample(DocSection.DataIndexing, "explorer-anchoring", "Read anchors, proofs and chain summaries through IAnchorExplorerService", Order = 1)]
        public async Task Given_IndexedAnchors_When_TheExplorerServiceIsQueried_Then_ItPagesAnchorsAndReturnsSummaries()
        {
            var anchors = new InMemoryAnchors();
            var summaries = new InMemorySummaries();
            for (var i = 1; i <= 30; i++)
                await anchors.UpsertAsync(new AnchorRecord { ChainId = 420420, EndBlock = i * 100 });
            await summaries.UpsertAsync(new ChainAnchoringSummary { ChainId = 420420, TotalAnchors = 30, LatestAnchoredBlock = 3000 });

            IAnchorExplorerService service = new AnchorExplorerService(
                anchors, new InMemoryRegistrations(), new InMemoryProofs(), summaries);

            var firstPage = await service.GetAnchorsAsync(420420);
            var secondPage = await service.GetAnchorsAsync(420420, page: 2, pageSize: 25);
            var count = await service.GetAnchorCountAsync(420420);
            var anchor = await service.GetAnchorByEndBlockAsync(420420, 300);
            var summary = await service.GetChainSummaryAsync(420420);
            var allSummaries = await service.GetAllChainSummariesAsync();

            Assert.True(service.IsConfigured);
            Assert.Equal(25, firstPage.Count);
            Assert.Equal(5, secondPage.Count);
            Assert.Equal(30, count);
            Assert.Equal(300, anchor!.EndBlock);
            Assert.Equal(3000, summary!.LatestAnchoredBlock);
            Assert.Single(allSummaries);
        }

        [Fact]
        [NethereumDocExample(DocSection.DataIndexing, "explorer-anchoring", "Register the anchoring pages without a connection string", Order = 2)]
        public async Task Given_NoConnectionString_When_AnchorExplorerServicesAreAdded_Then_TheServiceIsNotConfiguredAndNoNavItemsAreContributed()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ExplorerLocalizer>();
            services.AddAnchorExplorerServices();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var service = scope.ServiceProvider.GetRequiredService<IAnchorExplorerService>();
            var navItems = scope.ServiceProvider.GetRequiredService<IExplorerNavContributor>().GetNavItems();

            Assert.IsType<NullAnchorExplorerService>(service);
            Assert.False(service.IsConfigured);
            Assert.Empty(navItems);
            Assert.Empty(await service.GetAllChainSummariesAsync());
            Assert.Null(await service.GetChainSummaryAsync(420420));
        }

        [Fact]
        [NethereumDocExample(DocSection.DataIndexing, "explorer-anchoring", "Register the anchoring pages with a Postgres connection string", Order = 3)]
        public void Given_AConnectionString_When_AnchorExplorerServicesAreAdded_Then_TheServiceIsConfiguredAndTheAnchoringNavItemsAreContributed()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ExplorerLocalizer>();
            services.AddAnchorExplorerServices("Host=localhost;Database=anchors;Username=postgres;Password=postgres");
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            _ = scope.ServiceProvider.GetRequiredService<AnchoringLocalizer>();
            var service = scope.ServiceProvider.GetRequiredService<IAnchorExplorerService>();
            var navItems = scope.ServiceProvider.GetRequiredService<IExplorerNavContributor>().GetNavItems();

            Assert.IsType<AnchorExplorerService>(service);
            Assert.True(service.IsConfigured);
            Assert.Equal(new[] { "/anchoring", "/anchoring/admin" }, navItems.Select(n => n.Href).ToArray());
            Assert.Equal(new[] { 100, 101 }, navItems.Select(n => n.Order).ToArray());
        }
    }
}
