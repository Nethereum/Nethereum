using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Xunit;

namespace Nethereum.TokenServices.IntegrationTests
{
    public class CachingTests : IDisposable
    {
        private readonly string _tempDir;

        public CachingTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nethereum_cache_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public async Task MemoryCacheProvider_SetAndGet_Works()
        {
            var cache = new MemoryCacheProvider();

            await cache.SetAsync("key1", "value1");
            var result = await cache.GetAsync<string>("key1");

            Assert.Equal("value1", result);
        }

        [Fact]
        public async Task MemoryCacheProvider_Expiry_Works()
        {
            var cache = new MemoryCacheProvider();

            await cache.SetAsync("key1", "value1", TimeSpan.FromMilliseconds(50));

            var exists1 = await cache.ExistsAsync("key1");
            Assert.True(exists1);

            await Task.Delay(100);

            var exists2 = await cache.ExistsAsync("key1");
            Assert.False(exists2);
        }

        [Fact]
        public async Task MemoryCacheProvider_Remove_Works()
        {
            var cache = new MemoryCacheProvider();

            await cache.SetAsync("key1", "value1");
            await cache.RemoveAsync("key1");

            var exists = await cache.ExistsAsync("key1");
            Assert.False(exists);
        }

        [Fact]
        public async Task FileCacheProvider_SetAndGet_Works()
        {
            var cache = new FileCacheProvider(_tempDir);

            await cache.SetAsync("key1", "value1");
            var result = await cache.GetAsync<string>("key1");

            Assert.Equal("value1", result);
        }

        [Fact]
        public async Task FileCacheProvider_ComplexObject_Works()
        {
            var cache = new FileCacheProvider(_tempDir);

            var data = new TestData
            {
                Id = 123,
                Name = "Test",
                Values = new[] { 1, 2, 3 }
            };

            await cache.SetAsync("complex", data);
            var result = await cache.GetAsync<TestData>("complex");

            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
            Assert.Equal("Test", result.Name);
            Assert.Equal(3, result.Values.Length);
        }

        [Fact]
        public async Task FileCacheProvider_Expiry_Works()
        {
            var cache = new FileCacheProvider(_tempDir);

            await cache.SetAsync("key1", "value1", TimeSpan.FromMilliseconds(50));

            var exists1 = await cache.ExistsAsync("key1");
            Assert.True(exists1);

            await Task.Delay(100);

            var exists2 = await cache.ExistsAsync("key1");
            Assert.False(exists2);
        }

        [Fact]
        public async Task FileCacheProvider_Remove_Works()
        {
            var cache = new FileCacheProvider(_tempDir);

            await cache.SetAsync("key1", "value1");
            await cache.RemoveAsync("key1");

            var exists = await cache.ExistsAsync("key1");
            Assert.False(exists);
        }

        [Fact]
        public async Task FileCacheProvider_Persistence_Works()
        {
            var cache1 = new FileCacheProvider(_tempDir);
            await cache1.SetAsync("persistent", "data");

            var cache2 = new FileCacheProvider(_tempDir);
            var result = await cache2.GetAsync<string>("persistent");

            Assert.Equal("data", result);
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int[] Values { get; set; }
        }
    }
}
