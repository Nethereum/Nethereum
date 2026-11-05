using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round2
{
    public class S7R2InFlightRecoveryTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r2_inflight_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(string hash)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009",
                Priority = BigInteger.One,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x1111111111111111111111111111111111111111",
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = new byte[3],
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(1, 1),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[] { 0xab }
                }
            };
        }

        [Fact]
        [Trait("Rule", "S7-R2-4")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public async Task Constructor_WhenAnOpWasLeftInFlightByACrash_ReleasesItBackToPending()
        {
            var hash = "0x" + new string('a', 64);
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using (var manager = new BundlerRocksDbManager(options))
            {
                var mempool = new RocksDbUserOpMempool(manager, options);
                await mempool.AddAsync(Entry(hash));
                await mempool.MarkInFlightAsync(new[] { hash });
                Assert.Equal(MempoolEntryState.Submitted, (await mempool.GetAsync(hash))!.State);
            }

            using var reopened = new BundlerRocksDbManager(options);
            var reopenedMempool = new RocksDbUserOpMempool(reopened, options);

            Assert.Equal(MempoolEntryState.Pending, (await reopenedMempool.GetAsync(hash))!.State);
            Assert.Single(await reopenedMempool.GetPendingAsync(10));
        }
    }
}
