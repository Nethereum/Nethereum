using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPatriciaTriePerformanceTests : IAsyncLifetime
    {
        private RocksDbTestFixture _fixture = null!;
        private DevChainNode _node = null!;
        private readonly ITestOutputHelper _output;
        private readonly LegacyTransactionSigner _signer = new();

        public string PrivateKey { get; } = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string Address { get; } = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public BigInteger ChainId { get; } = 31337;
        public BigInteger InitialBalance { get; } = BigInteger.Parse("10000000000000000000000");

        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        public RocksDbPatriciaTriePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            _fixture = new RocksDbTestFixture();
            _node = CreateNode();
            await _node.StartAsync(new[] { Address }, InitialBalance);
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        private DevChainNode CreateNode()
        {
            return new DevChainNode(
                new DevChainConfig { ChainId = ChainId, AutoMine = true },
                _fixture.BlockStore,
                _fixture.TransactionStore,
                _fixture.ReceiptStore,
                _fixture.LogStore,
                _fixture.StateStore,
                _fixture.FilterStore,
                _fixture.TrieNodeStore);
        }

        private ISignedTransaction CreateContractDeploymentTransaction(byte[] bytecode, BigInteger? nonce = null)
        {
            var txNonce = nonce ?? _node.GetNonceAsync(Address).Result;
            BigInteger txGasPrice = 1_000_000_000;
            BigInteger txGasLimit = 3_000_000;

            var signedTxHex = _signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                "",
                BigInteger.Zero,
                txNonce,
                txGasPrice,
                txGasLimit,
                bytecode.ToHex());

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private ISignedTransaction CreateSignedTransaction(string to, BigInteger value, byte[] data = null, BigInteger? nonce = null)
        {
            var txNonce = nonce ?? _node.GetNonceAsync(Address).Result;
            BigInteger txGasPrice = 1_000_000_000;
            BigInteger txGasLimit = data != null ? 500_000 : 21_000;

            var signedTxHex = _signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                to,
                value,
                txNonce,
                txGasPrice,
                txGasLimit,
                data?.ToHex() ?? "");

            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private async Task<string> DeployERC20Async(BigInteger initialMint)
        {
            var bytecode = ERC20TestContract.GetDeploymentBytecode();
            var signedTx = CreateContractDeploymentTransaction(bytecode);
            var result = await _node.SendTransactionAsync(signedTx);

            if (!result.Success)
                throw new Exception($"ERC20 deployment failed: {result.RevertReason}");

            var receiptInfo = await _node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            var contractAddress = receiptInfo.ContractAddress;

            if (initialMint > 0)
            {
                var mintFunction = new MintFunction { To = Address, Amount = initialMint };
                var callData = mintFunction.GetCallData();
                var mintTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
                await _node.SendTransactionAsync(mintTx);
            }

            return contractAddress;
        }

        [Fact]
        public async Task RocksDB_ERC20_1000Transfers_DifferentAccounts_PerformanceTest()
        {
            const int transferCount = 1000;
            const int batchSize = 100;

            _output.WriteLine($"=== RocksDB ERC20 Performance Test: {transferCount} transfers to different accounts ===\n");

            var contractAddress = await DeployERC20Async(OneToken * 1_000_000);
            _output.WriteLine($"Deployed ERC20 at: {contractAddress}");

            var recipientAddresses = GenerateRandomAddresses(transferCount);
            _output.WriteLine($"Generated {transferCount} unique recipient addresses\n");

            var batchTimes = new List<double>();
            var stateRoots = new HashSet<string>();

            var initialBlock = await _node.GetLatestBlockAsync();
            stateRoots.Add(initialBlock.StateRoot.ToHex());

            var totalSw = Stopwatch.StartNew();

            for (int batch = 0; batch < transferCount / batchSize; batch++)
            {
                var batchSw = Stopwatch.StartNew();

                for (int i = 0; i < batchSize; i++)
                {
                    var txIndex = batch * batchSize + i;
                    var recipient = recipientAddresses[txIndex];

                    var transferFunction = new TransferFunction { To = recipient, Value = 100 };
                    var callData = transferFunction.GetCallData();
                    var signedTx = CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
                    var result = await _node.SendTransactionAsync(signedTx);

                    if (!result.Success)
                    {
                        _output.WriteLine($"  Tx {txIndex} FAILED: {result.RevertReason}");
                        continue;
                    }

                    var block = await _node.GetLatestBlockAsync();
                    stateRoots.Add(block.StateRoot.ToHex());
                }

                batchSw.Stop();
                var batchAvg = batchSw.ElapsedMilliseconds / (double)batchSize;
                batchTimes.Add(batchAvg);

                _output.WriteLine($"Batch {batch + 1}/{transferCount / batchSize}: " +
                    $"{batchSw.ElapsedMilliseconds}ms total, {batchAvg:F1}ms avg/tx");
            }

            totalSw.Stop();

            _output.WriteLine($"\n=== ROCKSDB RESULTS ===");
            _output.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total transactions: {transferCount}");
            _output.WriteLine($"Unique state roots: {stateRoots.Count}");
            _output.WriteLine($"Overall avg: {totalSw.ElapsedMilliseconds / (double)transferCount:F1}ms/tx");

            _output.WriteLine($"\n=== BATCH PERFORMANCE (checking for degradation) ===");
            for (int i = 0; i < batchTimes.Count; i++)
            {
                var degradation = i > 0 ? (batchTimes[i] / batchTimes[0] - 1) * 100 : 0;
                _output.WriteLine($"Batch {i + 1}: {batchTimes[i]:F1}ms avg " +
                    $"({(degradation >= 0 ? "+" : "")}{degradation:F1}% vs first batch)");
            }

            var firstBatchAvg = batchTimes.First();
            var lastBatchAvg = batchTimes.Last();
            var degradationPercent = ((lastBatchAvg / firstBatchAvg) - 1) * 100;

            _output.WriteLine($"\n=== DEGRADATION ANALYSIS ===");
            _output.WriteLine($"First batch avg: {firstBatchAvg:F1}ms");
            _output.WriteLine($"Last batch avg: {lastBatchAvg:F1}ms");
            _output.WriteLine($"Degradation: {degradationPercent:F1}%");

            if (degradationPercent > 100)
            {
                _output.WriteLine($"WARNING: Performance degraded by more than 100% - possible O(n) behavior!");
            }
            else if (degradationPercent > 50)
            {
                _output.WriteLine($"NOTICE: Some degradation observed, but within acceptable range for O(log n)");
            }
            else
            {
                _output.WriteLine($"GOOD: Minimal degradation - consistent with O(log n) incremental updates");
            }

            Assert.Equal(transferCount + 1, stateRoots.Count);
            Assert.True(degradationPercent < 200,
                $"Performance degraded by {degradationPercent:F1}% which suggests O(n) instead of O(log n)");
        }

        [Fact]
        public async Task RocksDB_MultipleContracts_StorageDistribution_PerformanceTest()
        {
            const int contractCount = 5;
            const int transfersPerContract = 100;

            _output.WriteLine($"=== RocksDB Multi-Contract Test: {contractCount} contracts, {transfersPerContract} transfers each ===\n");

            var contracts = new List<string>();
            for (int i = 0; i < contractCount; i++)
            {
                var addr = await DeployERC20Async(OneToken * 100_000);
                contracts.Add(addr);
                _output.WriteLine($"Deployed contract {i + 1}: {addr}");
            }

            var recipients = GenerateRandomAddresses(transfersPerContract);
            var roundTimes = new List<long>();
            var stateRoots = new HashSet<string>();

            _output.WriteLine($"\nRunning {transfersPerContract} rounds of {contractCount} transfers each...");

            var totalSw = Stopwatch.StartNew();

            for (int round = 0; round < transfersPerContract; round++)
            {
                var recipient = recipients[round];
                var roundSw = Stopwatch.StartNew();

                for (int c = 0; c < contractCount; c++)
                {
                    var transferFunction = new TransferFunction { To = recipient, Value = 10 };
                    var callData = transferFunction.GetCallData();
                    var signedTx = CreateSignedTransaction(contracts[c], BigInteger.Zero, callData);
                    var result = await _node.SendTransactionAsync(signedTx);

                    if (result.Success)
                    {
                        var block = await _node.GetLatestBlockAsync();
                        stateRoots.Add(block.StateRoot.ToHex());
                    }
                }

                roundSw.Stop();
                roundTimes.Add(roundSw.ElapsedMilliseconds);

                if ((round + 1) % 20 == 0)
                {
                    var last20 = roundTimes.TakeLast(20).Average();
                    _output.WriteLine($"Round {round + 1}/{transfersPerContract}: last 20 rounds avg = {last20:F1}ms/round");
                }
            }

            totalSw.Stop();

            _output.WriteLine($"\n=== ROCKSDB MULTI-CONTRACT RESULTS ===");
            _output.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total transactions: {contractCount * transfersPerContract}");
            _output.WriteLine($"Unique state roots: {stateRoots.Count}");
            _output.WriteLine($"Overall avg: {totalSw.ElapsedMilliseconds / (double)(contractCount * transfersPerContract):F1}ms/tx");

            var first20Rounds = roundTimes.Take(20).Average();
            var last20Rounds = roundTimes.TakeLast(20).Average();

            _output.WriteLine($"\n=== DEGRADATION ANALYSIS ===");
            _output.WriteLine($"First 20 rounds avg: {first20Rounds:F1}ms");
            _output.WriteLine($"Last 20 rounds avg: {last20Rounds:F1}ms");
            _output.WriteLine($"Ratio: {last20Rounds / first20Rounds:F2}x");

            Assert.True(last20Rounds / first20Rounds < 3.0,
                $"Performance ratio {last20Rounds / first20Rounds:F2}x is too high for O(log n)");
        }

        private static List<string> GenerateRandomAddresses(int count)
        {
            var addresses = new List<string>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[20];
                random.NextBytes(bytes);
                addresses.Add("0x" + bytes.ToHex());
            }

            return addresses;
        }
    }
}
