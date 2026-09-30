using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Reputation
{
    public class BundlerConcurrencyTests
    {
        private const string Address = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task RecordSeenAsync_UnderConcurrentIncrements_LosesNoUpdates()
        {
            const int increments = 2000;
            var service = new InMemoryReputationService();

            await Task.WhenAll(Enumerable.Range(0, increments)
                .Select(_ => Task.Run(() => service.RecordSeenAsync(Address, 1))));

            var entry = await service.GetAsync(Address);
            Assert.NotNull(entry);
            Assert.Equal(increments, entry!.OpsSeen);
        }

        [Fact]
        public async Task ReputationCounters_UnderMixedConcurrentAccess_StayConsistent()
        {
            const int perCounter = 1000;
            var service = new InMemoryReputationService();

            var seen = Enumerable.Range(0, perCounter)
                .Select(_ => Task.Run(() => service.RecordSeenAsync(Address, 1)));
            var included = Enumerable.Range(0, perCounter)
                .Select(_ => Task.Run(() => service.RecordIncludedAsync(Address)));
            var readers = Enumerable.Range(0, perCounter)
                .Select(_ => Task.Run(async () =>
                {
                    await service.GetAsync(Address);
                    await service.GetAllAsync();
                    await service.IsBannedAsync(Address);
                }));

            await Task.WhenAll(seen.Concat(included).Concat(readers));

            var entry = await service.GetAsync(Address);
            Assert.Equal(perCounter, entry!.OpsSeen);
            Assert.Equal(perCounter, entry.OpsIncluded);
        }

        [Fact]
        public async Task Mempool_GetBySender_DoesNotThrowWhileEntriesAreAddedAndRemoved()
        {
            const string sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var mempool = new InMemoryUserOpMempool(maxSize: 100000);
            var exceptions = new List<Exception>();
            var done = false;

            var writer = Task.Run(async () =>
            {
                for (var i = 0; i < 5000; i++)
                {
                    var entry = CreateEntry(sender, nonce: i, hash: "0x" + i.ToString("x64"));
                    await mempool.AddAsync(entry);
                    await mempool.RemoveAsync(entry.UserOpHash);
                }
                done = true;
            });

            var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                while (!done)
                {
                    try
                    {
                        await mempool.GetBySenderAsync(sender);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) exceptions.Add(ex);
                        return;
                    }
                }
            }));

            await Task.WhenAll(readers.Append(writer));

            Assert.Empty(exceptions);
        }

        private static MempoolEntry CreateEntry(string sender, int nonce, string hash)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = "0x0000000000000000000000000000000000000007",
                Priority = 1,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = nonce
                }
            };
        }
    }
}
