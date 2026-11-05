using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round2
{
    public class S7R2EvictionParityTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r2_evict_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(int i, int senderIndex, BigInteger nonce, BigInteger priority)
        {
            return new MempoolEntry
            {
                UserOpHash = "0x" + i.ToString("x64"),
                EntryPoint = EntryPoint,
                Priority = priority,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x" + senderIndex.ToString("x40"),
                    Nonce = nonce,
                    InitCode = Array.Empty<byte>(),
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(priority, priority + 1),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = Array.Empty<byte>()
                }
            };
        }

        private static async Task<List<string>> ScriptAsync(IUserOpMempool pool)
        {
            var log = new List<string>();
            log.Add((await pool.AddAsync(Entry(1, 1, 0, 5))).ToString());
            log.Add((await pool.AddAsync(Entry(2, 1, 1, 9))).ToString());
            log.Add((await pool.AddAsync(Entry(3, 2, 0, 7))).ToString());
            log.Add((await pool.AddAsync(Entry(4, 3, 0, 8))).ToString());
            log.Add("evicted:" + string.Join(",", ((IMempoolEvictionLog)pool).DrainEvicted().Select(e => e.UserOpHash.Substring(63))));
            log.Add((await pool.AddAsync(Entry(5, 4, 0, 1))).ToString());
            log.Add((await pool.AddAsync(Entry(6, 3, 1, 100))).ToString());
            log.Add("evicted:" + string.Join(",", ((IMempoolEvictionLog)pool).DrainEvicted().Select(e => e.UserOpHash.Substring(63))));
            log.Add("pending:" + string.Join(",", (await pool.GetAllPendingAsync()).Select(e => e.UserOpHash.Substring(63)).OrderBy(x => x)));
            return log;
        }

        [Fact]
        [Trait("Rule", "S7-R2-4")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task AddAsync_WhenTheMempoolIsFull_EvictsTheSameVictimsInBothBackends()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path, MaxMempoolSize = 3 };
            using var manager = new BundlerRocksDbManager(options);
            var rocks = new RocksDbUserOpMempool(manager, options);
            var memory = new InMemoryUserOpMempool(3);

            var memoryLog = await ScriptAsync(memory);
            var rocksLog = await ScriptAsync(rocks);

            Assert.Equal(memoryLog, rocksLog);
        }
    }
}
