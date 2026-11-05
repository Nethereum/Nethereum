using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round9
{
    public class S7SimplifyAdmissionParityTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r9_admission_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path)) Directory.Delete(_path, true);
        }

        private static MempoolEntry Entry(string hash, BigInteger nonce, BigInteger priority)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = priority,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x" + 7.ToString("x40"),
                    Nonce = nonce,
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

        private async Task WithBothBackends(Func<IUserOpMempool, Task> body)
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            await body(new InMemoryUserOpMempool(100));
            await body(new RocksDbUserOpMempool(manager, options));
        }

        [Fact]
        [Trait("Rule", "S7-R9-01")]
        public Task Add_ForTheSenderAndNonceOfAnOpWhoseBundleFailed_IsAddedOnBothBackends() =>
            WithBothBackends(async pool =>
            {
                await pool.AddAsync(Entry(Hash(1), 5, 5));
                await pool.MarkSubmittedAsync(new[] { Hash(1) }, Hash(99));
                await pool.MarkFailedAsync(new[] { Hash(1) }, "reverted");

                var outcome = await pool.AddAsync(Entry(Hash(2), 5, 5));

                Assert.Equal(MempoolAddOutcome.Added, outcome);
            });

        [Fact]
        [Trait("Rule", "S7-R9-02")]
        public Task Add_ForTheSenderAndNonceOfAnOpAlreadyIncluded_IsAddedOnBothBackends() =>
            WithBothBackends(async pool =>
            {
                await pool.AddAsync(Entry(Hash(1), 5, 5));
                await pool.MarkIncludedAsync(new[] { Hash(1) }, Hash(99), 10);

                var outcome = await pool.AddAsync(Entry(Hash(2), 5, 5));

                Assert.Equal(MempoolAddOutcome.Added, outcome);
            });

        [Fact]
        [Trait("Rule", "S7-R9-03")]
        public Task Add_ForTheSenderAndNonceOfASubmittedOp_IsRejectedAsDuplicateOnBothBackends() =>
            WithBothBackends(async pool =>
            {
                await pool.AddAsync(Entry(Hash(1), 5, 5));
                await pool.MarkSubmittedAsync(new[] { Hash(1) }, Hash(99));

                var outcome = await pool.AddAsync(Entry(Hash(2), 5, 50));

                Assert.Equal(MempoolAddOutcome.RejectedDuplicate, outcome);
            });

        [Fact]
        [Trait("Rule", "S7-R9-04")]
        public Task MarkFailed_ForAnIncludedOp_LeavesItIncludedOnBothBackends() =>
            WithBothBackends(async pool =>
            {
                await pool.AddAsync(Entry(Hash(1), 5, 5));
                await pool.MarkIncludedAsync(new[] { Hash(1) }, Hash(99), 10);

                await pool.MarkFailedAsync(new[] { Hash(1) }, "late");

                Assert.Equal(MempoolEntryState.Included, (await pool.GetAsync(Hash(1)))!.State);
            });
    }
}
