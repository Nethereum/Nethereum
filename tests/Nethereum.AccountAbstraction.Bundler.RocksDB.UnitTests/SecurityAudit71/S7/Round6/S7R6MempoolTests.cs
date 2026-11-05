using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests.SecurityAudit71.S7.Round6
{
    public class S7R6MempoolTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private const string TransactionHash = "0x1111111111111111111111111111111111111111111111111111111111111111";
        private const string BlockHash = "0x2222222222222222222222222222222222222222222222222222222222222222";
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"s7r6_mempool_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }

        private static MempoolEntry Entry(string hash, int senderIndex)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = 5,
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

        [Fact]
        [Trait("Rule", "S7-R6-05")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationByHash")]
        public async Task MarkIncluded_WhenTheOpIsAlreadyIncludedWithoutABlockHash_StoresTheBlockHashOfTheSecondCall()
        {
            var options = new BundlerRocksDbOptions { DatabasePath = _path };
            using var manager = new BundlerRocksDbManager(options);
            var mempool = new RocksDbUserOpMempool(manager, options);
            await mempool.AddAsync(Entry(Hash(1), 1));
            await mempool.MarkIncludedAsync(new[] { Hash(1) }, TransactionHash, 7, null);

            await mempool.MarkIncludedAsync(new[] { Hash(1) }, TransactionHash, 7, BlockHash);
            var stored = await mempool.GetAsync(Hash(1));

            Assert.NotNull(stored);
            Assert.Equal(BlockHash, stored!.BlockHash);
        }

        [Fact]
        [Trait("Rule", "S7-R6-02")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task MarkSubmitted_WhenTheOpIsAlreadyIncluded_KeepsItIncludedInTheInMemoryBackendAsInRocksDb()
        {
            var memory = new InMemoryUserOpMempool(100);
            await memory.AddAsync(Entry(Hash(1), 1));
            await memory.MarkIncludedAsync(new[] { Hash(1) }, TransactionHash, 7, BlockHash);

            await memory.MarkSubmittedAsync(new[] { Hash(1) }, TransactionHash);
            var stored = await memory.GetAsync(Hash(1));

            Assert.Equal(MempoolEntryState.Included, stored!.State);
        }
    }
}
