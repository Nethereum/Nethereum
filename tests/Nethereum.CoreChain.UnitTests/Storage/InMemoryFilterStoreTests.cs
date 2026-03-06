using System;
using System.Numerics;
using System.Reflection;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryFilterStoreTests
    {
        [Fact]
        public void CreateLogFilter_ReturnsUniqueFilterId()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();

            var id1 = store.CreateLogFilter(filter, 1);
            var id2 = store.CreateLogFilter(filter, 1);

            Assert.NotNull(id1);
            Assert.NotNull(id2);
            Assert.NotEqual(id1, id2);
            Assert.StartsWith("0x", id1);
            Assert.StartsWith("0x", id2);
        }

        [Fact]
        public void CreateLogFilter_StoresFilterState()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter
            {
                Addresses = new System.Collections.Generic.List<string> { "0x1234" },
                FromBlock = 10,
                ToBlock = 20
            };

            var filterId = store.CreateLogFilter(filter, 15);
            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.Equal(FilterType.Log, state.Type);
            Assert.NotNull(state.LogFilter);
            Assert.Equal(10, state.LastCheckedBlock); // Uses FromBlock when specified
        }

        [Fact]
        public void CreateLogFilter_UsesCurrentBlockWhenNoFromBlock()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter
            {
                Addresses = new System.Collections.Generic.List<string> { "0x1234" }
            };

            var filterId = store.CreateLogFilter(filter, 15);
            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.Equal(15, state.LastCheckedBlock); // Uses currentBlock when FromBlock not specified
        }

        [Fact]
        public void CreateBlockFilter_ReturnsUniqueFilterId()
        {
            var store = new InMemoryFilterStore();

            var id1 = store.CreateBlockFilter(1);
            var id2 = store.CreateBlockFilter(1);

            Assert.NotNull(id1);
            Assert.NotNull(id2);
            Assert.NotEqual(id1, id2);
        }

        [Fact]
        public void CreateBlockFilter_TracksLastCheckedBlock()
        {
            var store = new InMemoryFilterStore();
            BigInteger currentBlock = 42;

            var filterId = store.CreateBlockFilter(currentBlock);
            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.Equal(FilterType.Block, state.Type);
            Assert.Equal(42, state.LastCheckedBlock);
        }

        [Fact]
        public void CreatePendingTransactionFilter_ReturnsFilterId()
        {
            var store = new InMemoryFilterStore();

            var filterId = store.CreatePendingTransactionFilter();
            var state = store.GetFilter(filterId);

            Assert.NotNull(filterId);
            Assert.NotNull(state);
            Assert.Equal(FilterType.PendingTransaction, state.Type);
        }

        [Fact]
        public void GetFilter_ReturnsStoredFilter()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();

            var filterId = store.CreateLogFilter(filter, 1);
            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.Equal(filterId, state.Id);
        }

        [Fact]
        public void GetFilter_ReturnsNullForUnknownId()
        {
            var store = new InMemoryFilterStore();

            var state = store.GetFilter("0xnonexistent");

            Assert.Null(state);
        }

        [Fact]
        public void RemoveFilter_ReturnsTrueForExisting()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();
            var filterId = store.CreateLogFilter(filter, 1);

            var result = store.RemoveFilter(filterId);

            Assert.True(result);
        }

        [Fact]
        public void RemoveFilter_ReturnsFalseForUnknown()
        {
            var store = new InMemoryFilterStore();

            var result = store.RemoveFilter("0xnonexistent");

            Assert.False(result);
        }

        [Fact]
        public void RemoveFilter_PreventsSubsequentRetrieval()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();
            var filterId = store.CreateLogFilter(filter, 1);

            store.RemoveFilter(filterId);
            var state = store.GetFilter(filterId);

            Assert.Null(state);
        }

        [Fact]
        public void UpdateFilterLastBlock_UpdatesBlockNumber()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();
            var filterId = store.CreateLogFilter(filter, 10);

            store.UpdateFilterLastBlock(filterId, 50);
            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.Equal(50, state.LastCheckedBlock);
        }

        [Fact]
        public void FilterState_HasCreatedAtAndLastAccessedAt()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();

            var before = DateTime.UtcNow;
            var filterId = store.CreateLogFilter(filter, 1);
            var after = DateTime.UtcNow;

            var state = store.GetFilter(filterId);

            Assert.NotNull(state);
            Assert.True(state.CreatedAt >= before && state.CreatedAt <= after);
            Assert.True(state.LastAccessedAt >= before);
        }

        [Fact]
        public void GetFilter_RefreshesLastAccessedAt()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();
            var filterId = store.CreateLogFilter(filter, 1);

            var state1 = store.GetFilter(filterId);
            var firstAccess = state1.LastAccessedAt;

            System.Threading.Thread.Sleep(10);

            var state2 = store.GetFilter(filterId);

            Assert.True(state2.LastAccessedAt >= firstAccess);
        }

        [Fact]
        public void UpdateFilterLastBlock_RefreshesLastAccessedAt()
        {
            var store = new InMemoryFilterStore();
            var filter = new LogFilter();
            var filterId = store.CreateLogFilter(filter, 1);

            var state1 = store.GetFilter(filterId);
            var firstAccess = state1.LastAccessedAt;

            System.Threading.Thread.Sleep(10);
            store.UpdateFilterLastBlock(filterId, 100);

            var state2 = store.GetFilter(filterId);
            Assert.True(state2.LastAccessedAt >= firstAccess);
        }

        [Fact]
        public void FilterTtl_DefaultIsFiveMinutes()
        {
            var store = new InMemoryFilterStore();
            Assert.Equal(TimeSpan.FromMinutes(5), store.FilterTtl);
        }

        [Fact]
        public void FilterTtl_IsConfigurable()
        {
            var store = new InMemoryFilterStore();
            store.FilterTtl = TimeSpan.FromSeconds(30);
            Assert.Equal(TimeSpan.FromSeconds(30), store.FilterTtl);
        }

        [Fact]
        public void ExpiredFilter_EvictedAfterCleanupInterval()
        {
            var store = new InMemoryFilterStore();
            store.FilterTtl = TimeSpan.FromMilliseconds(1);

            var filterId = store.CreateLogFilter(new LogFilter(), 1);
            Assert.NotNull(store.GetFilter(filterId));

            System.Threading.Thread.Sleep(50);

            ForceCleanupInterval(store);

            var result = store.GetFilter(filterId);
            Assert.Null(result);
        }

        [Fact]
        public void NonExpiredFilter_NotEvicted()
        {
            var store = new InMemoryFilterStore();
            store.FilterTtl = TimeSpan.FromHours(1);

            var filterId = store.CreateLogFilter(new LogFilter(), 1);

            ForceCleanupInterval(store);

            var result = store.GetFilter(filterId);
            Assert.NotNull(result);
        }

        [Fact]
        public void MultipleFilters_OnlyExpiredEvicted()
        {
            var store = new InMemoryFilterStore();
            store.FilterTtl = TimeSpan.FromMilliseconds(1);

            var expiredId = store.CreateLogFilter(new LogFilter(), 1);

            System.Threading.Thread.Sleep(50);

            store.FilterTtl = TimeSpan.FromHours(1);
            var freshId = store.CreateLogFilter(new LogFilter(), 2);

            store.FilterTtl = TimeSpan.FromMilliseconds(1);
            ForceCleanupInterval(store);

            store.FilterTtl = TimeSpan.FromMilliseconds(1);
            var expiredResult = store.GetFilter(expiredId);

            store.FilterTtl = TimeSpan.FromHours(1);
            var freshResult = store.GetFilter(freshId);

            Assert.Null(expiredResult);
            Assert.NotNull(freshResult);
        }

        private void ForceCleanupInterval(InMemoryFilterStore store)
        {
            var field = typeof(InMemoryFilterStore).GetField("_lastCleanup", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(store, DateTime.UtcNow.AddMinutes(-5));
        }
    }
}
