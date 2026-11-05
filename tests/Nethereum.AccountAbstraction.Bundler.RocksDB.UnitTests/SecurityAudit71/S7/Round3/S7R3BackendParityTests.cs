using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round3
{
    public class S7R3BackendParityTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r3_parity_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(string hash, int senderIndex, BigInteger priority, ulong? validAfter = null, ulong? validUntil = null)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = priority,
                ValidAfter = validAfter,
                ValidUntil = validUntil,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x" + senderIndex.ToString("x40"),
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = new byte[3],
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(priority, priority + 1),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[] { 0xab }
                }
            };
        }

        private static string Hash(int i) => "0x" + i.ToString("x64");

        private static ulong NowSeconds() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        [Fact]
        [Trait("Rule", "S7-R3-01")]
        [Trait("Erc", "ERC-7562 bundle storage rules")]
        public async Task GetPending_AfterAnOpWithAccessedStorageAddressesWasStored_ReturnsTheAccessedStorageAddresses()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var mempool = new RocksDbUserOpMempool(manager, options);
            var entry = Entry(Hash(1), 1, 5);
            entry.AccessedStorageAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "0x" + new string('3', 40)
            };
            await mempool.AddAsync(entry);

            var pending = await mempool.GetPendingAsync(10);

            Assert.Single(pending);
            Assert.Equal(entry.AccessedStorageAddresses, pending[0].AccessedStorageAddresses);
        }

        [Fact]
        [Trait("Rule", "S7-R3-07")]
        [Trait("Erc", "ERC-4337 validity window")]
        public async Task GetAllPending_ForOpsWhoseValidityBoundIsTheCurrentSecond_AgreesWithTheInMemoryBackend()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(100);
            List<string>? memoryEligible = null;
            List<string>? rocksEligible = null;

            for (var attempt = 0; attempt < 200 && memoryEligible == null; attempt++)
            {
                await rocks.ClearAsync();
                await memory.ClearAsync();
                var second = NowSeconds();
                foreach (var pool in new IUserOpMempool[] { memory, rocks })
                {
                    await pool.AddAsync(Entry(Hash(1), 1, 5, validAfter: second));
                    await pool.AddAsync(Entry(Hash(2), 2, 5, validUntil: second));
                }

                var fromMemory = (await memory.GetAllPendingAsync()).Select(e => e.UserOpHash).OrderBy(x => x).ToList();
                var fromRocks = (await rocks.GetAllPendingAsync()).Select(e => e.UserOpHash).OrderBy(x => x).ToList();
                if (NowSeconds() == second)
                {
                    memoryEligible = fromMemory;
                    rocksEligible = fromRocks;
                }
            }

            Assert.NotNull(memoryEligible);
            Assert.Equal(memoryEligible, rocksEligible);
        }

        [Fact]
        [Trait("Rule", "S7-R3-08")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task AddAsync_WhenTwoOpsPayTheSameFeeAndTheMempoolIsFull_EvictsTheSameVictimInBothBackends()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path, MaxMempoolSize = 2 };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(2);
            var mismatches = new List<string>();

            for (var round = 0; round < 40; round++)
            {
                var victims = new List<string>();
                foreach (var pool in new IUserOpMempool[] { memory, rocks })
                {
                    await pool.ClearAsync();
                    await pool.AddAsync(Entry(Hash(1), 1, 5));
                    await pool.AddAsync(Entry(Hash(2), 2, 5));
                    await pool.AddAsync(Entry(Hash(3), 3, 50));
                    victims.Add(string.Join(",", ((IMempoolEvictionLog)pool).DrainEvicted().Select(e => e.UserOpHash.Substring(63))));
                }

                if (victims[0] != victims[1]) mismatches.Add($"round {round}: memory evicts {victims[0]}, rocksdb evicts {victims[1]}");
            }

            Assert.Empty(mismatches);
        }
    }
}
