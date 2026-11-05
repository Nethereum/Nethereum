using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7
{
    public class S7PersistenceTests : IDisposable
    {
        private readonly string _path;
        private readonly BundlerRocksDbOptions _options;
        private BundlerRocksDbManager _manager;

        public S7PersistenceTests()
        {
            _path = Path.Combine(Path.GetTempPath(), $"s7_bundler_{Guid.NewGuid():N}");
            _options = new BundlerRocksDbOptions { DatabasePath = _path };
            _manager = new BundlerRocksDbManager(_options);
        }

        public void Dispose()
        {
            _manager.Dispose();
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
                    CallData = new byte[] { 1, 2, 3 },
                    Signature = new byte[] { 0xab }
                }
            };
        }

        [Theory]
        [InlineData(new byte[] { 0x01, 0x02, 0x03 })]
        [InlineData(new byte[] { 0xc0 })]
        [InlineData(new byte[] { 0xc3, 0x01, 0x02, 0x03 })]
        [InlineData(new byte[] { 0xf9, 0xff, 0xff, 0x01 })]
        [Trait("Rule", "S7-13")]
        [Trait("Erc", "ERC-4337 mempool persistence")]
        public async Task GetPending_WhenOneStoredRowIsCorrupt_StillReturnsTheValidRows(byte[] corruptRow)
        {
            var mempool = new RocksDbUserOpMempool(_manager, _options);
            await mempool.AddAsync(Entry("0x" + new string('a', 64)));
            _manager.Put(
                BundlerRocksDbManager.CF_USEROP_PENDING,
                Encoding.UTF8.GetBytes("0x" + new string('b', 64)),
                corruptRow);

            var pending = await mempool.GetPendingAsync(10);

            Assert.Single(pending);
        }

        [Theory]
        [InlineData(new byte[] { 0x01, 0x02, 0x03 })]
        [InlineData(new byte[] { 0xc0 })]
        [Trait("Rule", "S7-14")]
        [Trait("Erc", "ERC-4337 reputation persistence")]
        public void ReputationStore_WhenOneStoredRowIsCorrupt_StillOpens(byte[] corruptRow)
        {
            _manager.Put(
                BundlerRocksDbManager.CF_REPUTATION,
                Encoding.UTF8.GetBytes("0x2222222222222222222222222222222222222222"),
                corruptRow);

            var store = new RocksDbReputationStore(_manager);

            Assert.NotNull(store);
        }
    }
}
