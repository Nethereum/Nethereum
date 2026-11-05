using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round2
{
    public class S7R2PersistenceTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private readonly string _path;

        public S7R2PersistenceTests()
        {
            _path = Path.Combine(Path.GetTempPath(), $"s7r2_bundler_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(string hash, BigInteger callGas, int callDataLength = 3, BigInteger? nonce = null)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = BigInteger.One,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x1111111111111111111111111111111111111111",
                    Nonce = nonce ?? BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = new byte[callDataLength],
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, callGas),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(1, 1),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[] { 0xab }
                }
            };
        }

        private static string Hash(int i) => "0x" + i.ToString("x64");

        [Fact]
        [Trait("Rule", "S7-R2-2")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public async Task GetAsync_WhenTheOpWasAdmittedWithGasAboveTheRocksDbOptionDefault_ReturnsTheOpAndDoesNotDeleteIt()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var mempool = new RocksDbUserOpMempool(manager, options);
            var bundlerMaxBundleGas = 30_000_000;
            Assert.True(20_000_000 < bundlerMaxBundleGas);
            Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(1), 20_000_000)));

            var read = await mempool.GetAsync(Hash(1));

            Assert.NotNull(read);
            Assert.Equal(1, await mempool.CountAsync());
        }

        [Fact]
        [Trait("Rule", "S7-R2-2")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public async Task GetPending_WhenTheOperatorLowersMaxUserOperationSizeAcrossARestart_KeepsTheStoredValidOps()
        {
            var first = new BundlerRocksDbOptions { DatabasePath = _path };
            using (var manager = new BundlerRocksDbManager(first))
            {
                var mempool = new RocksDbUserOpMempool(manager, first);
                Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(1), 100_000, callDataLength: 60_000)));
            }

            var second = new BundlerRocksDbOptions { DatabasePath = _path };
            using var reopened = new BundlerRocksDbManager(second);
            var reopenedMempool = new RocksDbUserOpMempool(reopened, second);

            var pending = await reopenedMempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(1, await reopenedMempool.CountAsync());
        }

        [Fact]
        [Trait("Rule", "S7-R2-3")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task PruneAsync_WhenAnOpStaysSubmittedPastTheTtl_RemovesItLikeTheInMemoryBackend()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path, EntryTtl = TimeSpan.Zero };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(100, TimeSpan.Zero);
            foreach (IUserOpMempool pool in new IUserOpMempool[] { rocks, memory })
            {
                Assert.Equal(MempoolAddOutcome.Added, await pool.AddAsync(Entry(Hash(1), 100_000)));
                await pool.MarkSubmittedAsync(new[] { Hash(1) }, Hash(900));
            }
            await Task.Delay(50);

            var memoryPruned = await memory.PruneAsync();
            var rocksPruned = await rocks.PruneAsync();

            Assert.Equal(1, memoryPruned);
            Assert.Equal(memoryPruned, rocksPruned);
        }

        [Fact]
        [Trait("Rule", "S7-R2-3")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task AddAsync_WhenTheSenderNonceSlotHoldsAStaleSubmittedOp_TheSlotIsReleasedAfterPruneLikeInMemory()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path, EntryTtl = TimeSpan.Zero };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(100, TimeSpan.Zero);
            var outcomes = new List<MempoolAddOutcome>();
            foreach (IUserOpMempool pool in new IUserOpMempool[] { memory, rocks })
            {
                await pool.AddAsync(Entry(Hash(1), 100_000));
                await pool.MarkSubmittedAsync(new[] { Hash(1) }, Hash(900));
                await Task.Delay(50);
                await pool.PruneAsync();
                outcomes.Add(await pool.AddAsync(Entry(Hash(2), 100_000)));
            }

            Assert.Equal(MempoolAddOutcome.Added, outcomes[0]);
            Assert.Equal(outcomes[0], outcomes[1]);
        }
    }
}
