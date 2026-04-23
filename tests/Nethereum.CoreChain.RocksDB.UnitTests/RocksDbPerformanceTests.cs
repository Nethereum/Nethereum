using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();
        private readonly string _basePath;

        public RocksDbPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _basePath = Path.Combine(Path.GetTempPath(), $"rocksdb_perf_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);
        }

        public void Dispose()
        {
            try { Directory.Delete(_basePath, true); } catch { }
        }

        [Fact]
        public async Task ScaleTest_RocksDbBackend()
        {
            BigInteger chainId = 31337;
            int blocks = 50;
            int txsPerBlock = 50;

            using var pNode = CreateRocksDbNode("patricia",
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false });
            using var bNode = CreateRocksDbNode("blake3",
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false,
                    StateTree = StateTreeType.Binary, StateTreeHashProvider = new Blake3HashProvider() });
            using var posNode = CreateRocksDbNode("poseidon",
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false,
                    StateTree = StateTreeType.Binary, StateTreeHashProvider = new BN254PoseidonPairHashProvider() });

            await pNode.StartAsync(new[] { _sender });
            await bNode.StartAsync(new[] { _sender });
            await posNode.StartAsync(new[] { _sender });

            ulong pN = 0, bN = 0, posN = 0;

            for (int w = 0; w < 3; w++)
            {
                for (int t = 0; t < txsPerBlock; t++)
                {
                    var to = $"0x{(w * txsPerBlock + t + 1):x40}";
                    await pNode.SendTransactionAsync(Tx(chainId, to, pN++));
                    await bNode.SendTransactionAsync(Tx(chainId, to, bN++));
                    await posNode.SendTransactionAsync(Tx(chainId, to, posN++));
                }
                await pNode.MineBlockAsync();
                await bNode.MineBlockAsync();
                await posNode.MineBlockAsync();
            }

            long totalP = 0, totalB = 0, totalPos = 0;
            _output.WriteLine($"RocksDB: {blocks} blocks × {txsPerBlock} txs/block");
            _output.WriteLine("Block | Patricia | Binary(Blake3) | Binary(Poseidon)");

            for (int b = 0; b < blocks; b++)
            {
                for (int t = 0; t < txsPerBlock; t++)
                {
                    var to = $"0x{((b + 3) * txsPerBlock + t + 1):x40}";
                    await pNode.SendTransactionAsync(Tx(chainId, to, pN++));
                    await bNode.SendTransactionAsync(Tx(chainId, to, bN++));
                    await posNode.SendTransactionAsync(Tx(chainId, to, posN++));
                }

                var swP = Stopwatch.StartNew(); await pNode.MineBlockAsync(); swP.Stop();
                var swB = Stopwatch.StartNew(); await bNode.MineBlockAsync(); swB.Stop();
                var swPos = Stopwatch.StartNew(); await posNode.MineBlockAsync(); swPos.Stop();

                totalP += swP.ElapsedMilliseconds;
                totalB += swB.ElapsedMilliseconds;
                totalPos += swPos.ElapsedMilliseconds;

                if ((b + 1) % 10 == 0 || b == blocks - 1)
                    _output.WriteLine($"  {b + 1,3} | {swP.ElapsedMilliseconds,6}ms | {swB.ElapsedMilliseconds,6}ms | {swPos.ElapsedMilliseconds,6}ms");
            }

            _output.WriteLine($"TOTAL | {totalP,6}ms | {totalB,6}ms | {totalPos,6}ms");
            _output.WriteLine($"  AVG | {totalP / blocks,6}ms | {totalB / blocks,6}ms | {totalPos / blocks,6}ms");

            var pSize = DirSize(Path.Combine(_basePath, "patricia"));
            var bSize = DirSize(Path.Combine(_basePath, "blake3"));
            var posSize = DirSize(Path.Combine(_basePath, "poseidon"));
            _output.WriteLine($" SIZE | {pSize / 1024,5}KB | {bSize / 1024,5}KB | {posSize / 1024,5}KB");
        }

        private DevChainNode CreateRocksDbNode(string name, DevChainConfig config)
        {
            var dbPath = Path.Combine(_basePath, name);
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
            var blockStore = new RocksDbBlockStore(manager);
            return new DevChainNode(
                config,
                blockStore,
                new RocksDbTransactionStore(manager, blockStore),
                new RocksDbReceiptStore(manager, blockStore),
                new RocksDbLogStore(manager),
                new HistoricalStateStore(new RocksDbStateStore(manager),
                    new RocksDbStateDiffStore(manager), HistoricalStateOptions.DevChainDefault),
                new RocksDbFilterStore(manager),
                new RocksDbTrieNodeStore(manager));
        }

        private ISignedTransaction Tx(BigInteger chainId, string to, ulong nonce)
        {
            return TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId, to, 1000, nonce, 1_000_000_000, 21_000, ""));
        }

        private static long DirSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
    }
}
