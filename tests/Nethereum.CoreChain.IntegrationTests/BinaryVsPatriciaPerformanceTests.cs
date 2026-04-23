using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BinaryVsPatriciaPerformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public BinaryVsPatriciaPerformanceTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task PerBlockTiming_PatriciaVsBinaryBlake3()
        {
            BigInteger chainId = 31337;
            int blocks = 10;
            int txsPerBlock = 20;

            var pNode = await Start(StateTreeType.Patricia, null, chainId);
            var bNode = await Start(StateTreeType.Binary, new Blake3HashProvider(), chainId);
            var posNode = await Start(StateTreeType.Binary, new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider(), chainId);

            ulong pN = 0, bN = 0, posN = 0;

            // Warm up: 5 blocks to let JIT tiered compilation promote hot paths
            int warmupBlocks = 5;
            for (int w = 0; w < warmupBlocks; w++)
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

            _output.WriteLine("Block | Patricia | Binary(Blake3) | Binary(Poseidon)");

            for (int b = 0; b < blocks; b++)
            {
                for (int t = 0; t < txsPerBlock; t++)
                {
                    var to = $"0x{(b * txsPerBlock + t + 1):x40}";
                    await pNode.SendTransactionAsync(Tx(chainId, to, pN++));
                    await bNode.SendTransactionAsync(Tx(chainId, to, bN++));
                    await posNode.SendTransactionAsync(Tx(chainId, to, posN++));
                }

                var swP = Stopwatch.StartNew();
                await pNode.MineBlockAsync();
                swP.Stop();

                var swB = Stopwatch.StartNew();
                await bNode.MineBlockAsync();
                swB.Stop();

                var swPos = Stopwatch.StartNew();
                await posNode.MineBlockAsync();
                swPos.Stop();

                _output.WriteLine($"  {b + 1,3} | {swP.ElapsedMilliseconds,6}ms | {swB.ElapsedMilliseconds,6}ms | {swPos.ElapsedMilliseconds,6}ms");
            }

            pNode.Dispose();
            bNode.Dispose();
            posNode.Dispose();
        }

        private async Task<DevChainNode> Start(
            StateTreeType tree, Nethereum.Util.HashProviders.IHashProvider hash, BigInteger chainId)
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false,
                StateTree = tree, StateTreeHashProvider = hash
            });
            await node.StartAsync(new[] { _sender });
            return node;
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        public async Task ScaleTest_TransfersPerBlock(int blocks, int txsPerBlock)
        {
            BigInteger chainId = 31337;

            var pNode = await Start(StateTreeType.Patricia, null, chainId);
            var bNode = await Start(StateTreeType.Binary, new Blake3HashProvider(), chainId);
            var posNode = await Start(StateTreeType.Binary, new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider(), chainId);

            ulong pN = 0, bN = 0, posN = 0;

            // Warmup
            for (int w = 0; w < 5; w++)
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
            _output.WriteLine($"Scale test: {blocks} blocks × {txsPerBlock} txs/block");
            _output.WriteLine("Block | Patricia | Binary(Blake3) | Binary(Poseidon)");

            for (int b = 0; b < blocks; b++)
            {
                for (int t = 0; t < txsPerBlock; t++)
                {
                    var to = $"0x{((b + 5) * txsPerBlock + t + 1):x40}";
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

            pNode.Dispose();
            bNode.Dispose();
            posNode.Dispose();
        }

        [Fact]
        public async Task ScaleTest_SqliteBackend()
        {
            BigInteger chainId = 31337;
            int blocks = 50;
            int txsPerBlock = 50;

            var pPath = Path.Combine(Path.GetTempPath(), $"perf_patricia_{Guid.NewGuid():N}");
            var bPath = Path.Combine(Path.GetTempPath(), $"perf_blake3_{Guid.NewGuid():N}");
            var posPath = Path.Combine(Path.GetTempPath(), $"perf_poseidon_{Guid.NewGuid():N}");

            var pNode = new DevChainNode(
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false },
                pPath);
            var bNode = new DevChainNode(
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false,
                    StateTree = StateTreeType.Binary, StateTreeHashProvider = new Blake3HashProvider() },
                bPath);
            var posNode = new DevChainNode(
                new DevChainConfig { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false,
                    StateTree = StateTreeType.Binary,
                    StateTreeHashProvider = new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider() },
                posPath);

            await pNode.StartAsync(new[] { _sender });
            await bNode.StartAsync(new[] { _sender });
            await posNode.StartAsync(new[] { _sender });

            ulong pN = 0, bN = 0, posN = 0;

            // Warmup
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
            _output.WriteLine($"SQLite: {blocks} blocks × {txsPerBlock} txs/block");
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

            pNode.Dispose();
            bNode.Dispose();
            posNode.Dispose();

            try
            {
                var pSize = new DirectoryInfo(pPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                var bSize = new DirectoryInfo(bPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                var posSize = new DirectoryInfo(posPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                _output.WriteLine($" SIZE | {pSize / 1024,5}KB | {bSize / 1024,5}KB | {posSize / 1024,5}KB");
            }
            catch { }
        }

        private ISignedTransaction Tx(BigInteger chainId, string to, ulong nonce)
        {
            return TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId, to, 1000, nonce, 1_000_000_000, 21_000, ""));
        }
    }
}
