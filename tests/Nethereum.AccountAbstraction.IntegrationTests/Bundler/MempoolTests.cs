using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    public class MempoolTests
    {
        private static MempoolEntry CreateTestEntry(
            string userOpHash,
            string sender = "0x1234567890123456789012345678901234567890",
            BigInteger? priority = null,
            ulong? validAfter = null,
            ulong? validUntil = null,
            BigInteger? verificationGas = null,
            BigInteger? callGas = null)
        {
            var vGas = verificationGas ?? 100_000;
            var cGas = callGas ?? 100_000;

            var accountGasLimits = new byte[32];
            var vGasBytes = vGas.ToByteArray();
            Array.Reverse(vGasBytes);
            Array.Copy(vGasBytes, 0, accountGasLimits, 16 - vGasBytes.Length, vGasBytes.Length);

            var cGasBytes = cGas.ToByteArray();
            Array.Reverse(cGasBytes);
            Array.Copy(cGasBytes, 0, accountGasLimits, 32 - cGasBytes.Length, cGasBytes.Length);

            var gasFees = new byte[32];
            var priorityFee = priority ?? 1_000_000_000;
            var maxFee = priorityFee * 2;

            var priorityBytes = ((BigInteger)priorityFee).ToByteArray();
            Array.Reverse(priorityBytes);
            Array.Copy(priorityBytes, 0, gasFees, 16 - priorityBytes.Length, priorityBytes.Length);

            var maxFeeBytes = ((BigInteger)maxFee).ToByteArray();
            Array.Reverse(maxFeeBytes);
            Array.Copy(maxFeeBytes, 0, gasFees, 32 - maxFeeBytes.Length, maxFeeBytes.Length);

            return new MempoolEntry
            {
                UserOpHash = userOpHash,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = accountGasLimits,
                    PreVerificationGas = 21_000,
                    GasFees = gasFees,
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[65]
                },
                EntryPoint = "0x0000000071727De22E5E9d8BAf0edAc6f37da032",
                Priority = priority ?? 1_000_000_000,
                Prefund = 1_000_000_000_000_000,
                ValidAfter = validAfter,
                ValidUntil = validUntil
            };
        }

        [Fact]
        public async Task AddAsync_WithValidEntry_ReturnsTrue()
        {
            var mempool = new InMemoryUserOpMempool();
            var entry = CreateTestEntry("0x" + new string('1', 64));

            var result = await mempool.AddAsync(entry);

            Assert.True(result);
        }

        [Fact]
        public async Task AddAsync_WithDuplicateHash_ReturnsFalse()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('2', 64);
            var entry1 = CreateTestEntry(hash, "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry(hash, "0x2222222222222222222222222222222222222222");

            var result1 = await mempool.AddAsync(entry1);
            var result2 = await mempool.AddAsync(entry2);

            Assert.True(result1);
            Assert.False(result2);
        }

        [Fact]
        public async Task AddAsync_WhenFull_ReturnsFalse()
        {
            var mempool = new InMemoryUserOpMempool(maxSize: 2);

            var entry1 = CreateTestEntry("0x" + new string('1', 64));
            var entry2 = CreateTestEntry("0x" + new string('2', 64));
            var entry3 = CreateTestEntry("0x" + new string('3', 64));

            await mempool.AddAsync(entry1);
            await mempool.AddAsync(entry2);
            var result = await mempool.AddAsync(entry3);

            Assert.False(result);
        }

        [Fact]
        public async Task GetAsync_WithExistingHash_ReturnsEntry()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('4', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);

            var result = await mempool.GetAsync(hash);

            Assert.NotNull(result);
            Assert.Equal(hash, result.UserOpHash);
        }

        [Fact]
        public async Task GetAsync_WithNonExistingHash_ReturnsNull()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('5', 64);

            var result = await mempool.GetAsync(hash);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsOnlyPendingEntries()
        {
            var mempool = new InMemoryUserOpMempool();

            var entry1 = CreateTestEntry("0x" + new string('1', 64));
            var entry2 = CreateTestEntry("0x" + new string('2', 64));
            await mempool.AddAsync(entry1);
            await mempool.AddAsync(entry2);

            await mempool.MarkSubmittedAsync(new[] { entry1.UserOpHash }, "0xtx1");

            var pending = await mempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(entry2.UserOpHash, pending[0].UserOpHash);
        }

        [Fact]
        public async Task GetPendingAsync_OrdersByPriorityDescending()
        {
            var mempool = new InMemoryUserOpMempool();

            var lowPriority = CreateTestEntry("0x" + new string('1', 64), priority: 1_000_000_000);
            var highPriority = CreateTestEntry("0x" + new string('2', 64), priority: 5_000_000_000);
            var medPriority = CreateTestEntry("0x" + new string('3', 64), priority: 3_000_000_000);

            await mempool.AddAsync(lowPriority);
            await mempool.AddAsync(highPriority);
            await mempool.AddAsync(medPriority);

            var pending = await mempool.GetPendingAsync(10);

            Assert.Equal(3, pending.Length);
            Assert.Equal(highPriority.UserOpHash, pending[0].UserOpHash);
            Assert.Equal(medPriority.UserOpHash, pending[1].UserOpHash);
            Assert.Equal(lowPriority.UserOpHash, pending[2].UserOpHash);
        }

        [Fact]
        public async Task GetPendingAsync_RespectsMaxCount()
        {
            var mempool = new InMemoryUserOpMempool();

            for (int i = 0; i < 10; i++)
            {
                var entry = CreateTestEntry($"0x{i:D64}");
                await mempool.AddAsync(entry);
            }

            var pending = await mempool.GetPendingAsync(5);

            Assert.Equal(5, pending.Length);
        }

        [Fact]
        public async Task GetPendingAsync_RespectsMaxGas()
        {
            var mempool = new InMemoryUserOpMempool();

            var entry1 = CreateTestEntry("0x" + new string('1', 64));
            var entry2 = CreateTestEntry("0x" + new string('2', 64));

            await mempool.AddAsync(entry1);
            await mempool.AddAsync(entry2);

            var pending = await mempool.GetPendingAsync(10, maxGas: 250_000);

            Assert.Single(pending);
        }

        [Fact]
        public async Task GetPendingAsync_ExcludesEntriesBeforeValidAfter()
        {
            var mempool = new InMemoryUserOpMempool();

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var futureEntry = CreateTestEntry("0x" + new string('1', 64), validAfter: now + 3600);
            var currentEntry = CreateTestEntry("0x" + new string('2', 64), validAfter: now - 10);

            await mempool.AddAsync(futureEntry);
            await mempool.AddAsync(currentEntry);

            var pending = await mempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(currentEntry.UserOpHash, pending[0].UserOpHash);
        }

        [Fact]
        public async Task GetPendingAsync_ExcludesEntriesAfterValidUntil()
        {
            var mempool = new InMemoryUserOpMempool();

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiredEntry = CreateTestEntry("0x" + new string('1', 64), validUntil: now - 10);
            var validEntry = CreateTestEntry("0x" + new string('2', 64), validUntil: now + 3600);

            await mempool.AddAsync(expiredEntry);
            await mempool.AddAsync(validEntry);

            var pending = await mempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(validEntry.UserOpHash, pending[0].UserOpHash);
        }

        [Fact]
        public async Task GetBySenderAsync_ReturnsAllEntriesForSender()
        {
            var mempool = new InMemoryUserOpMempool();

            var sender1 = "0x1111111111111111111111111111111111111111";
            var sender2 = "0x2222222222222222222222222222222222222222";

            var entry1 = CreateTestEntry("0x" + new string('1', 64), sender: sender1);
            var entry2 = CreateTestEntry("0x" + new string('2', 64), sender: sender1);
            var entry3 = CreateTestEntry("0x" + new string('3', 64), sender: sender2);

            await mempool.AddAsync(entry1);
            await mempool.AddAsync(entry2);
            await mempool.AddAsync(entry3);

            var sender1Entries = await mempool.GetBySenderAsync(sender1);
            var sender2Entries = await mempool.GetBySenderAsync(sender2);

            Assert.Equal(2, sender1Entries.Length);
            Assert.Single(sender2Entries);
        }

        [Fact]
        public async Task RemoveAsync_WithExistingHash_ReturnsTrue()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('6', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);

            var result = await mempool.RemoveAsync(hash);

            Assert.True(result);

            var retrieved = await mempool.GetAsync(hash);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task RemoveAsync_WithNonExistingHash_ReturnsFalse()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('7', 64);

            var result = await mempool.RemoveAsync(hash);

            Assert.False(result);
        }

        [Fact]
        public async Task MarkSubmittedAsync_ChangesStateToSubmitted()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('8', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);

            await mempool.MarkSubmittedAsync(new[] { hash }, "0xtxhash");

            var result = await mempool.GetAsync(hash);
            Assert.NotNull(result);
            Assert.Equal(MempoolEntryState.Submitted, result.State);
            Assert.Equal("0xtxhash", result.TransactionHash);
        }

        [Fact]
        public async Task MarkIncludedAsync_ChangesStateToIncluded()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('9', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);
            await mempool.MarkSubmittedAsync(new[] { hash }, "0xtxhash");

            await mempool.MarkIncludedAsync(new[] { hash }, "0xtxhash", 12345);

            var result = await mempool.GetAsync(hash);
            Assert.NotNull(result);
            Assert.Equal(MempoolEntryState.Included, result.State);
            Assert.Equal(12345, result.BlockNumber);
        }

        [Fact]
        public async Task MarkFailedAsync_ChangesStateToFailed()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('a', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);

            await mempool.MarkFailedAsync(new[] { hash }, "Test error");

            var result = await mempool.GetAsync(hash);
            Assert.NotNull(result);
            Assert.Equal(MempoolEntryState.Failed, result.State);
            Assert.Equal("Test error", result.Error);
        }

        [Fact]
        public async Task RevertSubmittedAsync_ReturnsTooPendingState()
        {
            var mempool = new InMemoryUserOpMempool();
            var hash = "0x" + new string('b', 64);
            var entry = CreateTestEntry(hash);
            await mempool.AddAsync(entry);
            await mempool.MarkSubmittedAsync(new[] { hash }, "0xtxhash");

            await mempool.RevertSubmittedAsync("0xtxhash");

            var result = await mempool.GetAsync(hash);
            Assert.NotNull(result);
            Assert.Equal(MempoolEntryState.Pending, result.State);
            Assert.Null(result.TransactionHash);
            Assert.Equal(1, result.RetryCount);
        }

        [Fact]
        public async Task ClearAsync_RemovesAllEntries()
        {
            var mempool = new InMemoryUserOpMempool();

            for (int i = 0; i < 5; i++)
            {
                var entry = CreateTestEntry($"0x{i:D64}");
                await mempool.AddAsync(entry);
            }

            var countBefore = await mempool.CountAsync();
            Assert.Equal(5, countBefore);

            await mempool.ClearAsync();

            var countAfter = await mempool.CountAsync();
            Assert.Equal(0, countAfter);
        }

        [Fact]
        public async Task GetStatsAsync_ReturnsAccurateStats()
        {
            var mempool = new InMemoryUserOpMempool();

            var entry1 = CreateTestEntry("0x" + new string('1', 64), sender: "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry("0x" + new string('2', 64), sender: "0x2222222222222222222222222222222222222222");
            var entry3 = CreateTestEntry("0x" + new string('3', 64), sender: "0x1111111111111111111111111111111111111111");

            await mempool.AddAsync(entry1);
            await mempool.AddAsync(entry2);
            await mempool.AddAsync(entry3);

            await mempool.MarkSubmittedAsync(new[] { entry1.UserOpHash }, "0xtx1");
            await mempool.MarkFailedAsync(new[] { entry2.UserOpHash }, "error");

            var stats = await mempool.GetStatsAsync();

            Assert.Equal(3, stats.TotalCount);
            Assert.Equal(1, stats.PendingCount);
            Assert.Equal(1, stats.SubmittedCount);
            Assert.Equal(1, stats.FailedCount);
            Assert.Equal(2, stats.UniqueSenders);
        }

        [Fact]
        public async Task PruneAsync_RemovesOldPendingEntries()
        {
            var mempool = new InMemoryUserOpMempool(entryTtl: TimeSpan.FromMilliseconds(100));

            var entry = CreateTestEntry("0x" + new string('c', 64));
            await mempool.AddAsync(entry);

            await Task.Delay(200);

            var pruned = await mempool.PruneAsync();

            Assert.Equal(1, pruned);

            var retrieved = await mempool.GetAsync(entry.UserOpHash);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task PruneAsync_KeepsRecentIncludedEntries()
        {
            var mempool = new InMemoryUserOpMempool(entryTtl: TimeSpan.FromMilliseconds(100));

            var entry = CreateTestEntry("0x" + new string('d', 64));
            await mempool.AddAsync(entry);
            await mempool.MarkIncludedAsync(new[] { entry.UserOpHash }, "0xtx", 1);

            await Task.Delay(200);

            var pruned = await mempool.PruneAsync();

            Assert.Equal(0, pruned);

            var retrieved = await mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
        }

        [Fact]
        public async Task CountAsync_ReturnsCorrectCount()
        {
            var mempool = new InMemoryUserOpMempool();

            Assert.Equal(0, await mempool.CountAsync());

            for (int i = 0; i < 3; i++)
            {
                var entry = CreateTestEntry($"0x{i:D64}");
                await mempool.AddAsync(entry);
            }

            Assert.Equal(3, await mempool.CountAsync());

            await mempool.RemoveAsync("0x0000000000000000000000000000000000000000000000000000000000000000");

            Assert.Equal(2, await mempool.CountAsync());
        }
    }
}
