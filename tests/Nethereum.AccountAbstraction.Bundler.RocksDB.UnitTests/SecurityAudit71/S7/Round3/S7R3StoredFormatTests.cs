using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round3
{
    public class S7R3StoredFormatTests : IDisposable
    {
        private const int RoundTwoElementCount = 18;
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r3_format_{Guid.NewGuid():N}");

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

        private static byte[] AsRoundTwoRow(MempoolEntry entry)
        {
            var elements = (RLPCollection)RLP.RLP.Decode(MempoolEntrySerializer.Serialize(entry));
            return RLP.RLP.EncodeList(
                elements.Take(RoundTwoElementCount).Select(e => RLP.RLP.EncodeElement(e.RLPData)).ToArray());
        }

        [Fact]
        [Trait("Rule", "S7-R3-01")]
        [Trait("Rule", "S7-R3-08")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public async Task GetAsync_WhenTheRowWasWrittenInTheRoundTwoFormat_ReadsItWithMillisecondSubmittedAtAndNoStorageAddresses()
        {
            var hash = "0x" + new string('a', 64);
            var entry = Entry(hash);
            entry.SubmittedAt = new DateTimeOffset(2026, 10, 27, 12, 0, 0, 123, TimeSpan.Zero).AddTicks(4567);
            entry.AccessedStorageAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0x" + new string('3', 40) };
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var mempool = new RocksDbUserOpMempool(manager, options);
            manager.Put(
                BundlerRocksDbManager.CF_USEROP_PENDING,
                System.Text.Encoding.UTF8.GetBytes(hash),
                AsRoundTwoRow(entry));

            var read = await mempool.GetAsync(hash);

            Assert.NotNull(read);
            Assert.Equal(entry.SubmittedAt.ToUnixTimeMilliseconds(), read!.SubmittedAt.ToUnixTimeMilliseconds());
            Assert.Empty(read.AccessedStorageAddresses);
            Assert.Null(read.FinishedAt);
        }

        [Fact]
        [Trait("Rule", "S7-R3-08")]
        [Trait("Rule", "S7-R3-12")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public void Deserialize_WhenTheRowWasWrittenInTheCurrentFormat_KeepsTickPrecisionAndFinishedAt()
        {
            var entry = Entry("0x" + new string('b', 64));
            entry.SubmittedAt = new DateTimeOffset(2026, 10, 27, 12, 0, 0, 123, TimeSpan.Zero).AddTicks(4567);
            entry.FinishedAt = entry.SubmittedAt.AddSeconds(3).AddTicks(11);

            var read = MempoolEntrySerializer.Deserialize(MempoolEntrySerializer.Serialize(entry));

            Assert.Equal(entry.SubmittedAt, read!.SubmittedAt);
            Assert.Equal(entry.FinishedAt, read.FinishedAt);
        }
    }
}
