using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Catalog;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;
using Xunit;

namespace Nethereum.TokenServices.IntegrationTests
{
    public class TokenCatalogRepositoryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FileTokenCatalogRepository _repository;

        public TokenCatalogRepositoryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nethereum_catalog_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _repository = new FileTokenCatalogRepository(_tempDir);
        }

        public void Dispose()
        {
            _repository.Dispose();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public async Task AddAndGetTokens_RoundTrip()
        {
            var tokens = new List<CatalogTokenInfo>
            {
                new CatalogTokenInfo { Address = "0x1111111111111111111111111111111111111111", Symbol = "TKN1", Name = "Token One", Decimals = 18 },
                new CatalogTokenInfo { Address = "0x2222222222222222222222222222222222222222", Symbol = "TKN2", Name = "Token Two", Decimals = 8 }
            };

            var added = await _repository.AddOrUpdateTokensAsync(1, tokens);
            Assert.Equal(2, added);

            var retrieved = await _repository.GetAllTokensAsync(1);
            Assert.Equal(2, retrieved.Count);
            Assert.Contains(retrieved, t => t.Symbol == "TKN1");
            Assert.Contains(retrieved, t => t.Symbol == "TKN2");

            var single = await _repository.GetTokenByAddressAsync(1, "0x1111111111111111111111111111111111111111");
            Assert.NotNull(single);
            Assert.Equal("TKN1", single.Symbol);
        }

        [Fact]
        public async Task GetTokensAddedSince_FiltersCorrectly()
        {
            var oldTime = DateTime.UtcNow.AddHours(-2);
            var midTime = DateTime.UtcNow.AddHours(-1);

            var oldToken = new CatalogTokenInfo
            {
                Address = "0x1111111111111111111111111111111111111111",
                Symbol = "OLD",
                AddedAtUtc = oldTime
            };

            var newToken = new CatalogTokenInfo
            {
                Address = "0x2222222222222222222222222222222222222222",
                Symbol = "NEW",
                AddedAtUtc = DateTime.UtcNow
            };

            await _repository.AddOrUpdateTokensAsync(1, new[] { oldToken, newToken });

            var tokensSinceMid = await _repository.GetTokensAddedSinceAsync(1, midTime);

            Assert.Single(tokensSinceMid);
            Assert.Equal("NEW", tokensSinceMid[0].Symbol);
        }

        [Fact]
        public async Task SeedFromEmbedded_PopulatesCatalog()
        {
            await _repository.SeedFromEmbeddedAsync(1);

            var isInit = await _repository.IsInitializedAsync(1);
            Assert.True(isInit);

            var count = await _repository.GetTokenCountAsync(1);
            Assert.True(count > 0, "Should have seeded tokens from embedded data");

            var tokens = await _repository.GetAllTokensAsync(1);
            Assert.All(tokens, t => Assert.Equal("embedded", t.Source));
        }

        [Fact]
        public async Task AddOrUpdate_UpdatesExistingWhenFlagSet()
        {
            var token = new CatalogTokenInfo
            {
                Address = "0x1111111111111111111111111111111111111111",
                Symbol = "OLD_SYM",
                Name = "Old Name"
            };

            await _repository.AddOrUpdateTokensAsync(1, new[] { token });

            var updatedToken = new CatalogTokenInfo
            {
                Address = "0x1111111111111111111111111111111111111111",
                Symbol = "NEW_SYM",
                Name = "New Name"
            };

            await _repository.AddOrUpdateTokensAsync(1, new[] { updatedToken }, updateExisting: false);
            var noUpdate = await _repository.GetTokenByAddressAsync(1, token.Address);
            Assert.Equal("OLD_SYM", noUpdate.Symbol);

            await _repository.AddOrUpdateTokensAsync(1, new[] { updatedToken }, updateExisting: true);
            var withUpdate = await _repository.GetTokenByAddressAsync(1, token.Address);
            Assert.Equal("NEW_SYM", withUpdate.Symbol);
            Assert.NotNull(withUpdate.UpdatedAtUtc);
        }

        [Fact]
        public async Task Metadata_PersistsCorrectly()
        {
            var metadata = new CatalogMetadata
            {
                IsSeeded = true,
                SeededAtUtc = DateTime.UtcNow,
                LastRefreshUtc = DateTime.UtcNow,
                LastRefreshSource = "test",
                TokenCount = 100
            };

            await _repository.SetMetadataAsync(1, metadata);

            var retrieved = await _repository.GetMetadataAsync(1);
            Assert.True(retrieved.IsSeeded);
            Assert.Equal("test", retrieved.LastRefreshSource);
            Assert.Equal(100, retrieved.TokenCount);
        }
    }

    public class TokenCatalogRefreshServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FileTokenCatalogRepository _repository;
        private readonly TokenCatalogRefreshService _refreshService;
        private readonly MockRefreshSource _mockSource;

        public TokenCatalogRefreshServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nethereum_refresh_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _repository = new FileTokenCatalogRepository(_tempDir);
            _mockSource = new MockRefreshSource();
            _refreshService = new TokenCatalogRefreshService(_repository, new[] { _mockSource });
        }

        public void Dispose()
        {
            _repository.Dispose();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public async Task RefreshAsync_AddsNewTokens()
        {
            _mockSource.TokensToReturn = new List<CatalogTokenInfo>
            {
                new CatalogTokenInfo { Address = "0x1111111111111111111111111111111111111111", Symbol = "NEW1" },
                new CatalogTokenInfo { Address = "0x2222222222222222222222222222222222222222", Symbol = "NEW2" }
            };

            var result = await _refreshService.RefreshAsync(1, new CatalogRefreshOptions { ForceRefresh = true });

            Assert.True(result.Success);
            Assert.Equal(2, result.TotalTokensAdded);
            Assert.Equal("mock", result.SourceUsed);

            var tokens = await _repository.GetAllTokensAsync(1);
            Assert.True(tokens.Count >= 2);
        }

        [Fact]
        public async Task RefreshAsync_SkipsWhenNotDue()
        {
            var metadata = new CatalogMetadata
            {
                IsSeeded = true,
                LastRefreshUtc = DateTime.UtcNow
            };
            await _repository.SetMetadataAsync(1, metadata);

            var result = await _refreshService.RefreshAsync(1);

            Assert.True(result.WasSkipped);
            Assert.Contains("interval", result.SkipReason.ToLower());
        }

        [Fact]
        public async Task RefreshAsync_ForcesWhenRequested()
        {
            var metadata = new CatalogMetadata
            {
                IsSeeded = true,
                LastRefreshUtc = DateTime.UtcNow
            };
            await _repository.SetMetadataAsync(1, metadata);

            _mockSource.TokensToReturn = new List<CatalogTokenInfo>
            {
                new CatalogTokenInfo { Address = "0x3333333333333333333333333333333333333333", Symbol = "FORCED" }
            };

            var result = await _refreshService.RefreshAsync(1, new CatalogRefreshOptions { ForceRefresh = true });

            Assert.True(result.Success);
            Assert.False(result.WasSkipped);
        }

        private class MockRefreshSource : ITokenCatalogRefreshSource
        {
            public string SourceName => "mock";
            public int Priority => 1;
            public List<CatalogTokenInfo> TokensToReturn { get; set; } = new List<CatalogTokenInfo>();

            public Task<bool> SupportsChainAsync(long chainId, CancellationToken ct = default)
                => Task.FromResult(true);

            public Task<TokenCatalogRefreshResult> FetchTokensAsync(long chainId, DateTime? sinceUtc = null, CancellationToken ct = default)
            {
                return Task.FromResult(new TokenCatalogRefreshResult
                {
                    Success = true,
                    Tokens = TokensToReturn,
                    NewTokenCount = TokensToReturn.Count,
                    SourceName = SourceName
                });
            }

            public Task<RateLimitInfo> GetRateLimitInfoAsync(CancellationToken ct = default)
                => Task.FromResult(new RateLimitInfo { IsRateLimited = false });
        }
    }
}
