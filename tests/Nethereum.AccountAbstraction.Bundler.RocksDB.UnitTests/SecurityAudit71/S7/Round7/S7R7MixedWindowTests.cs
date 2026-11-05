using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round7
{
    public class S7R7MixedWindowTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r7_window_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(string hash, int senderIndex, ulong? validUntil, ulong? afterBlock, ulong? untilBlock)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = 5,
                ValidUntil = validUntil,
                ValidAfterBlock = afterBlock,
                ValidUntilBlock = untilBlock,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x" + senderIndex.ToString("x40"),
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = new byte[3],
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(5, 6),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[] { 0xab }
                }
            };
        }

        private static string Hash(int i) => "0x" + i.ToString("x64");

        private static ulong Now() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData")]
        public async Task Select_WhenTheAccountTimeRangeExpiredAndThePaymasterBlockRangeIsOpen_DoesNotSelectItAndPrunesItInBothStores()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(100);
            var results = new List<string>();

            foreach (IUserOpMempool pool in new IUserOpMempool[] { memory, rocks })
            {
                await pool.AddAsync(Entry(Hash(1), 1, Now() - 10, 0, 1_000_000_000UL));
                var selected = (await pool.GetPendingAsync(10)).Length;
                var pruned = await pool.PruneAsync();
                results.Add($"selected={selected} pruned={pruned} remaining={await pool.CountAsync()}");
            }

            Assert.Equal("selected=0 pruned=1 remaining=0", results[0]);
            Assert.Equal(results[0], results[1]);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData")]
        public async Task GetPending_WhenTheOpHasOnlyABlockWindow_IsNotHiddenByTheTimeWindowInEitherStore()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(100);

            foreach (IUserOpMempool pool in new IUserOpMempool[] { memory, rocks })
            {
                await pool.AddAsync(Entry(Hash(1), 1, null, 5, 10));

                Assert.Single(await pool.GetPendingAsync(10));
                Assert.Equal(0, await pool.PruneAsync());
            }
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData")]
        public async Task GetAsync_WhenTheBlockWindowWasStored_ReadsItBackIncludingABlockZeroBound()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            await rocks.AddAsync(Entry(Hash(1), 1, null, 0, 77));

            var read = await rocks.GetAsync(Hash(1));

            Assert.Equal(0UL, read!.ValidAfterBlock);
            Assert.Equal(77UL, read.ValidUntilBlock);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData")]
        public void Deserialize_WhenTheRowWasWrittenBeforeTheBlockWindow_ReadsNoBlockWindow()
        {
            var entry = Entry(Hash(1), 1, null, 3, 9);
            entry.SubmittedAt = DateTimeOffset.UtcNow;
            var elements = (RLPCollection)RLP.RLP.Decode(MempoolEntrySerializer.Serialize(entry));
            var oldRow = RLP.RLP.EncodeList(
                elements.Take(21).Select(e => RLP.RLP.EncodeElement(e.RLPData)).ToArray());

            var read = MempoolEntrySerializer.Deserialize(oldRow);

            Assert.Null(read!.ValidAfterBlock);
            Assert.Null(read.ValidUntilBlock);
        }
    }
}
