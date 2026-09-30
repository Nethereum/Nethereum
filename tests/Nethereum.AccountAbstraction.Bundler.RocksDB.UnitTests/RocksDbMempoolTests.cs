using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests
{
    public class RocksDbMempoolTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly BundlerRocksDbManager _manager;
        private readonly RocksDbUserOpMempool _mempool;

        public RocksDbMempoolTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"bundler_test_{Guid.NewGuid():N}");
            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            _manager = new BundlerRocksDbManager(options);
            _mempool = new RocksDbUserOpMempool(_manager, options);
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }

        private MempoolEntry CreateTestEntry(string userOpHash, string sender = "0x1111111111111111111111111111111111111111")
        {
            return new MempoolEntry
            {
                UserOpHash = userOpHash,
                EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009", // v0.9
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = BigInteger.Zero,
                    CallData = new byte[] { 0x01, 0x02, 0x03 },
                    Signature = new byte[] { 0xab, 0xcd }
                },
                Priority = BigInteger.One
            };
        }

        [Fact]
        public async Task AddAsync_WithValidEntry_ReturnsAddedAndPersists()
        {
            var entry = CreateTestEntry("0x" + new string('a', 64));

            var result = await _mempool.AddAsync(entry);

            Assert.Equal(MempoolAddOutcome.Added, result);
            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(entry.UserOpHash, retrieved.UserOpHash);
            Assert.Equal(entry.EntryPoint, retrieved.EntryPoint);
            Assert.Equal(MempoolEntryState.Pending, retrieved.State);
        }

        [Fact]
        public async Task AddAsync_WithDuplicateHash_ReturnsRejectedDuplicate()
        {
            var entry = CreateTestEntry("0x" + new string('b', 64));
            await _mempool.AddAsync(entry);

            var duplicate = CreateTestEntry("0x" + new string('b', 64));
            var result = await _mempool.AddAsync(duplicate);

            Assert.Equal(MempoolAddOutcome.RejectedDuplicate, result);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsOnlyPendingEntries()
        {
            var entry1 = CreateTestEntry("0x" + new string('c', 64), "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry("0x" + new string('d', 64), "0x2222222222222222222222222222222222222222");
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var pending = await _mempool.GetPendingAsync(10);

            Assert.Equal(2, pending.Length);
        }

        [Fact]
        public async Task MarkSubmittedAsync_ChangesState()
        {
            var entry = CreateTestEntry("0x" + new string('e', 64));
            await _mempool.AddAsync(entry);

            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, "0x" + new string('f', 64));

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Submitted, retrieved.State);
        }

        [Fact]
        public async Task MarkIncludedAsync_ChangesStateAndSetsBlockNumber()
        {
            var entry = CreateTestEntry("0x" + new string('1', 64));
            await _mempool.AddAsync(entry);
            var txHash = "0x" + new string('2', 64);
            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, txHash);

            await _mempool.MarkIncludedAsync(new[] { entry.UserOpHash }, txHash, 12345);

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Included, retrieved.State);
            Assert.Equal(12345, retrieved.BlockNumber);
        }

        [Fact]
        public async Task RevertSubmittedAsync_ReturnsEntryToPending()
        {
            var entry = CreateTestEntry("0x" + new string('3', 64));
            await _mempool.AddAsync(entry);
            var txHash = "0x" + new string('4', 64);
            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, txHash);

            await _mempool.RevertSubmittedAsync(txHash);

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Pending, retrieved.State);
            Assert.Equal(1, retrieved.RetryCount);
        }

        [Fact]
        public async Task GetBySenderAsync_ReturnsEntriesForSender()
        {
            var sender = "0x2222222222222222222222222222222222222222";
            var entry1 = CreateTestEntry("0x" + new string('5', 64), sender);
            entry1.UserOperation.Nonce = BigInteger.Zero;
            var entry2 = CreateTestEntry("0x" + new string('6', 64), sender);
            entry2.UserOperation.Nonce = BigInteger.One;
            var entry3 = CreateTestEntry("0x" + new string('7', 64), "0x3333333333333333333333333333333333333333");

            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);
            await _mempool.AddAsync(entry3);

            var bySender = await _mempool.GetBySenderAsync(sender);

            Assert.Equal(2, bySender.Length);
            Assert.All(bySender, e => Assert.Equal(sender.ToLowerInvariant(), e.UserOperation.Sender?.ToLowerInvariant()));
        }

        [Fact]
        public async Task RemoveAsync_DeletesEntry()
        {
            var entry = CreateTestEntry("0x" + new string('8', 64));
            await _mempool.AddAsync(entry);

            var removed = await _mempool.RemoveAsync(entry.UserOpHash);

            Assert.True(removed);
            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task CountAsync_ReturnsCorrectCount()
        {
            await _mempool.AddAsync(CreateTestEntry("0x" + new string('9', 64), "0x1111111111111111111111111111111111111111"));
            await _mempool.AddAsync(CreateTestEntry("0x" + new string('0', 64), "0x2222222222222222222222222222222222222222"));

            var count = await _mempool.CountAsync();

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task GetStatsAsync_ReturnsAccurateStats()
        {
            var entry1 = CreateTestEntry("0x" + new string('a', 64), "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry("0x" + new string('b', 64), "0x2222222222222222222222222222222222222222");
            entry2.Paymaster = "0x4444444444444444444444444444444444444444";
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var stats = await _mempool.GetStatsAsync();

            Assert.Equal(2, stats.TotalCount);
            Assert.Equal(2, stats.PendingCount);
            Assert.Equal(2, stats.UniqueSenders);
            Assert.Equal(1, stats.UniquePaymasters);
        }

        private static byte[] PackGasFees(ulong maxPriorityFeePerGas, ulong maxFeePerGas)
        {
            var packed = new byte[32];
            var priority = BitConverter.GetBytes(maxPriorityFeePerGas);
            var max = BitConverter.GetBytes(maxFeePerGas);
            Array.Reverse(priority);
            Array.Reverse(max);
            Buffer.BlockCopy(priority, 0, packed, 8, 8);
            Buffer.BlockCopy(max, 0, packed, 24, 8);
            return packed;
        }

        [Fact]
        public async Task AddAsync_SameSenderNonceWithTenPercentFeeBump_ReplacesPendingOp()
        {
            var original = CreateTestEntry("0x" + new string('e', 63) + "1");
            original.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            Assert.Equal(MempoolAddOutcome.Added, await _mempool.AddAsync(original));

            var replacement = CreateTestEntry("0x" + new string('e', 63) + "2");
            replacement.UserOperation.GasFees = PackGasFees(1_100_000_000, 2_200_000_000);

            Assert.Equal(MempoolAddOutcome.Replaced, await _mempool.AddAsync(replacement));

            Assert.Null(await _mempool.GetAsync(original.UserOpHash));
            Assert.NotNull(await _mempool.GetAsync(replacement.UserOpHash));

            var pending = await _mempool.GetPendingAsync(10);
            Assert.Single(pending);
            Assert.Equal(replacement.UserOpHash, pending[0].UserOpHash);
        }

        [Fact]
        public async Task AddAsync_SameSenderNonceWithoutFeeBump_RejectsUnderpriced()
        {
            var original = CreateTestEntry("0x" + new string('f', 63) + "1");
            original.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            Assert.Equal(MempoolAddOutcome.Added, await _mempool.AddAsync(original));

            var samePriced = CreateTestEntry("0x" + new string('f', 63) + "2");
            samePriced.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            Assert.Equal(MempoolAddOutcome.RejectedUnderpriced, await _mempool.AddAsync(samePriced));

            var onlyPriorityBumped = CreateTestEntry("0x" + new string('f', 63) + "3");
            onlyPriorityBumped.UserOperation.GasFees = PackGasFees(1_100_000_000, 2_000_000_000);
            Assert.Equal(MempoolAddOutcome.RejectedUnderpriced, await _mempool.AddAsync(onlyPriorityBumped));

            Assert.NotNull(await _mempool.GetAsync(original.UserOpHash));
        }

        [Fact]
        public async Task AddAsync_SameSenderNonceWhileSubmitted_RejectsDuplicate()
        {
            var original = CreateTestEntry("0x" + new string('a', 63) + "1");
            original.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            await _mempool.AddAsync(original);
            await _mempool.MarkSubmittedAsync(new[] { original.UserOpHash }, "0x" + new string('a', 63) + "9");

            var replacement = CreateTestEntry("0x" + new string('a', 63) + "2");
            replacement.UserOperation.GasFees = PackGasFees(2_000_000_000, 4_000_000_000);

            Assert.Equal(MempoolAddOutcome.RejectedDuplicate, await _mempool.AddAsync(replacement));
        }

        [Fact]
        public async Task GetPendingAsync_MultipleNoncesSameSender_ReturnsFullContiguousChain()
        {
            var sender = "0x5555555555555555555555555555555555555555";
            var entry1 = CreateTestEntry("0x" + new string('b', 63) + "1", sender);
            entry1.UserOperation.Nonce = BigInteger.Zero;
            var entry2 = CreateTestEntry("0x" + new string('b', 63) + "2", sender);
            entry2.UserOperation.Nonce = BigInteger.One;

            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var pending = await _mempool.GetPendingAsync(10);

            Assert.Equal(2, pending.Length);
            Assert.Equal(BigInteger.Zero, pending[0].UserOperation.Nonce);
            Assert.Equal(BigInteger.One, pending[1].UserOperation.Nonce);
        }

        [Fact]
        public async Task GetPendingAsync_MiddleNonceOutsideValidityWindow_TruncatesAtGap()
        {
            var sender = "0x5656565656565656565656565656565656565656";
            var longExpired = 1UL;

            var entry0 = CreateTestEntry("0x" + new string('b', 63) + "3", sender);
            entry0.UserOperation.Nonce = BigInteger.Zero;

            var entry1 = CreateTestEntry("0x" + new string('b', 63) + "4", sender);
            entry1.UserOperation.Nonce = BigInteger.One;
            entry1.ValidUntil = longExpired;

            var entry2 = CreateTestEntry("0x" + new string('b', 63) + "5", sender);
            entry2.UserOperation.Nonce = 2;

            await _mempool.AddAsync(entry0);
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var pending = await _mempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(entry0.UserOpHash, pending[0].UserOpHash);
        }

        [Fact]
        public async Task GetPendingAsync_SenderWithTwoIndependentNonceKeys_ReturnsBothContiguousRuns()
        {
            var sender = "0x5757575757575757575757575757575757575757";

            BigInteger key0 = 0;
            BigInteger key1 = 1;

            var key0Entry0 = CreateTestEntry("0x" + new string('b', 63) + "6", sender);
            key0Entry0.UserOperation.Nonce = (key0 << 64) | 0;
            var key0Entry1 = CreateTestEntry("0x" + new string('b', 63) + "7", sender);
            key0Entry1.UserOperation.Nonce = (key0 << 64) | 1;

            var key1Entry0 = CreateTestEntry("0x" + new string('b', 63) + "8", sender);
            key1Entry0.UserOperation.Nonce = (key1 << 64) | 0;
            var key1Entry1 = CreateTestEntry("0x" + new string('b', 63) + "9", sender);
            key1Entry1.UserOperation.Nonce = (key1 << 64) | 1;

            await _mempool.AddAsync(key0Entry0);
            await _mempool.AddAsync(key0Entry1);
            await _mempool.AddAsync(key1Entry0);
            await _mempool.AddAsync(key1Entry1);

            var pending = await _mempool.GetPendingAsync(10);

            // A sender's independent 2D-nonce keys (ERC-4337 nonce >> 64) are legitimately
            // non-adjacent - grouping by (sender, nonceKey) rather than sender alone must not
            // truncate one key's contiguous run because of the other key's nonce values.
            Assert.Equal(4, pending.Length);
            var byHash = pending.ToDictionary(e => e.UserOpHash);
            Assert.Contains(key0Entry0.UserOpHash, byHash.Keys);
            Assert.Contains(key0Entry1.UserOpHash, byHash.Keys);
            Assert.Contains(key1Entry0.UserOpHash, byHash.Keys);
            Assert.Contains(key1Entry1.UserOpHash, byHash.Keys);
        }

        [Fact]
        public async Task RemoveAsync_AfterReplacement_KeepsIndexPointingAtReplacement()
        {
            var original = CreateTestEntry("0x" + new string('d', 63) + "1");
            original.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            await _mempool.AddAsync(original);

            var replacement = CreateTestEntry("0x" + new string('d', 63) + "2");
            replacement.UserOperation.GasFees = PackGasFees(1_100_000_000, 2_200_000_000);
            await _mempool.AddAsync(replacement);

            await _mempool.RemoveAsync(original.UserOpHash);

            var bySender = await _mempool.GetBySenderAsync(replacement.UserOperation.Sender!);
            Assert.Single(bySender);
            Assert.Equal(replacement.UserOpHash, bySender[0].UserOpHash);
        }

        [Fact]
        public async Task MarkIncludedAsync_FromFailedState_HealsToIncluded()
        {
            var entry = CreateTestEntry("0x" + new string('c', 63) + "9");
            await _mempool.AddAsync(entry);
            await _mempool.MarkFailedAsync(new[] { entry.UserOpHash }, "AA25: receipt timeout race");

            var txHash = "0x" + new string('d', 63) + "9";
            var blockHash = "0x" + new string('e', 63) + "9";

            await _mempool.MarkIncludedAsync(new[] { entry.UserOpHash }, txHash, 42, blockHash);

            var healed = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(healed);
            Assert.Equal(MempoolEntryState.Included, healed!.State);
            Assert.Equal(txHash, healed.TransactionHash);
            Assert.Equal(42, healed.BlockNumber);
            Assert.Equal(blockHash, healed.BlockHash);
            Assert.Null(healed.Error);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "RocksDB-backed mempool survives a process restart")]
        public async Task DataPersistsAcrossManagerRecreation()
        {
            var entry = CreateTestEntry("0x" + new string('c', 64));
            await _mempool.AddAsync(entry);

            _manager.Dispose();

            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            using var newManager = new BundlerRocksDbManager(options);
            var newMempool = new RocksDbUserOpMempool(newManager, options);

            var retrieved = await newMempool.GetAsync(entry.UserOpHash);

            Assert.NotNull(retrieved);
            Assert.Equal(entry.UserOpHash, retrieved.UserOpHash);
        }

        private static byte[] CreateLegacySenderKey(string sender, BigInteger nonce)
        {
            var senderBytes = sender.ToLowerInvariant().HexToByteArray();
            var nonceBytes = nonce.ToBytesForRLPEncoding();
            var paddedNonce = new byte[32];
            Buffer.BlockCopy(nonceBytes, 0, paddedNonce, 32 - nonceBytes.Length, nonceBytes.Length);

            var result = new byte[senderBytes.Length + paddedNonce.Length];
            Buffer.BlockCopy(senderBytes, 0, result, 0, senderBytes.Length);
            Buffer.BlockCopy(paddedNonce, 0, result, senderBytes.Length, paddedNonce.Length);
            return result;
        }

        private static List<byte[]> ReadSenderIndexKeys(BundlerRocksDbManager manager)
        {
            var keys = new List<byte[]>();
            using var iterator = manager.CreateIterator(BundlerRocksDbManager.CF_SENDER_INDEX);
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                keys.Add(iterator.Key());
                iterator.Next();
            }
            return keys;
        }

        [Fact]
        public async Task StoreOpen_WithLegacyFormatSenderIndexRow_RebuildsIndexAndEnforcesRule()
        {
            var sender = "0x6666666666666666666666666666666666666666";
            var op = CreateTestEntry("0x" + new string('7', 63) + "1", sender);
            op.UserOperation.Nonce = BigInteger.Zero;
            op.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            await _mempool.AddAsync(op);

            var legacyKey = CreateLegacySenderKey(sender, BigInteger.Zero);
            var opHashKey = MempoolEntrySerializer.StringToKey(op.UserOpHash);
            var currentKey = MempoolEntrySerializer.CreateSenderKey(sender, op.EntryPoint, BigInteger.Zero);
            _manager.Delete(BundlerRocksDbManager.CF_SENDER_INDEX, currentKey);
            _manager.Put(BundlerRocksDbManager.CF_SENDER_INDEX, legacyKey, opHashKey);
            _manager.Delete(
                BundlerRocksDbManager.CF_METADATA,
                Encoding.UTF8.GetBytes(RocksDbUserOpMempool.SenderIndexSchemaVersionMetadataKey));

            Assert.Equal(52, legacyKey.Length);
            Assert.Contains(ReadSenderIndexKeys(_manager), k => k.Length == 52);

            _manager.Dispose();

            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            using var newManager = new BundlerRocksDbManager(options);
            var newMempool = new RocksDbUserOpMempool(newManager, options);

            var keysAfter = ReadSenderIndexKeys(newManager);
            Assert.DoesNotContain(keysAfter, k => k.Length == 52);
            Assert.All(keysAfter, k => Assert.Equal(72, k.Length));

            Assert.NotNull(await newMempool.GetAsync(op.UserOpHash));

            var samePriced = CreateTestEntry("0x" + new string('7', 63) + "2", sender);
            samePriced.UserOperation.Nonce = BigInteger.Zero;
            samePriced.UserOperation.GasFees = PackGasFees(1_000_000_000, 2_000_000_000);
            Assert.Equal(MempoolAddOutcome.RejectedUnderpriced, await newMempool.AddAsync(samePriced));

            var bumped = CreateTestEntry("0x" + new string('7', 63) + "3", sender);
            bumped.UserOperation.Nonce = BigInteger.Zero;
            bumped.UserOperation.GasFees = PackGasFees(1_100_000_000, 2_200_000_000);
            Assert.Equal(MempoolAddOutcome.Replaced, await newMempool.AddAsync(bumped));

            newManager.Dispose();
        }

        [Fact]
        public async Task StoreOpen_AlreadyCurrentSchema_IsNoOpAndPreservesIndex()
        {
            var entry1 = CreateTestEntry("0x" + new string('8', 63) + "1", "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry("0x" + new string('8', 63) + "2", "0x2222222222222222222222222222222222222222");
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var before = ReadSenderIndexKeys(_manager)
                .Select(k => k.ToHex())
                .OrderBy(h => h)
                .ToList();

            _manager.Dispose();

            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            using var newManager = new BundlerRocksDbManager(options);
            var newMempool = new RocksDbUserOpMempool(newManager, options);

            var after = ReadSenderIndexKeys(newManager)
                .Select(k => k.ToHex())
                .OrderBy(h => h)
                .ToList();
            Assert.Equal(before, after);
            Assert.All(ReadSenderIndexKeys(newManager), k => Assert.Equal(72, k.Length));

            Assert.NotNull(await newMempool.GetAsync(entry1.UserOpHash));
            Assert.NotNull(await newMempool.GetAsync(entry2.UserOpHash));

            var marker = newManager.Get(
                BundlerRocksDbManager.CF_METADATA,
                Encoding.UTF8.GetBytes(RocksDbUserOpMempool.SenderIndexSchemaVersionMetadataKey));
            Assert.NotNull(marker);
            Assert.Equal(RocksDbUserOpMempool.SenderIndexSchemaVersion, BitConverter.ToInt32(marker, 0));

            newManager.Dispose();
        }
    }
}
