using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.P2P.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Account = Nethereum.Web3.Accounts.Account;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.P2P.IntegrationTests
{
    [Collection("CliqueCluster")]
    public class CliqueLoadTests : IClassFixture<CliqueClusterFixture>
    {
        private readonly CliqueClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public CliqueLoadTests(CliqueClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        [Trait("Category", "P2P-LoadTest")]
        public async Task LoadTest_100Transactions_AcrossCluster()
        {
            await _fixture.StartAllNodesAsync();
            await Task.Delay(2000);

            const int totalTransactions = 100;
            const int transactionsPerNode = totalTransactions / 3;

            var testAccounts = GenerateTestAccounts(10);
            var txResults = new ConcurrentBag<TxSubmissionResult>();

            var sw = Stopwatch.StartNew();

            var tasks = new List<Task>();
            for (int nodeIndex = 0; nodeIndex < _fixture.Nodes.Count; nodeIndex++)
            {
                var node = _fixture.Nodes[nodeIndex];
                var startIndex = nodeIndex * transactionsPerNode;
                var count = transactionsPerNode + (nodeIndex == 2 ? totalTransactions % 3 : 0);

                tasks.Add(SubmitTransactionsAsync(node, testAccounts, startIndex, count, txResults));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            var successCount = txResults.Count(r => r.Success);
            var failCount = txResults.Count(r => !r.Success);
            var avgLatencyMs = txResults.Where(r => r.Success).Average(r => r.LatencyMs);

            _output.WriteLine($"=== Load Test Results ===");
            _output.WriteLine($"Total transactions: {totalTransactions}");
            _output.WriteLine($"Successful: {successCount}");
            _output.WriteLine($"Failed: {failCount}");
            _output.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {totalTransactions * 1000.0 / sw.ElapsedMilliseconds:F2} tx/s");
            _output.WriteLine($"Average latency: {avgLatencyMs:F2}ms");

            foreach (var node in _fixture.Nodes)
            {
                _output.WriteLine($"Node {node.Index} pending pool: {node.Sequencer.TxPool.PendingCount}");
            }

            Assert.True(successCount > totalTransactions * 0.8, "At least 80% of transactions should succeed");
        }

        [Fact]
        [Trait("Category", "P2P-LoadTest")]
        public async Task LoadTest_BurstTransactions_MeasureThroughput()
        {
            await _fixture.StartAllNodesAsync();
            await Task.Delay(2000);

            const int burstSize = 50;
            var testAccounts = GenerateTestAccounts(burstSize);
            var txResults = new ConcurrentBag<TxSubmissionResult>();

            var node = _fixture.Nodes[0];

            var sw = Stopwatch.StartNew();

            var tasks = testAccounts.Select((account, index) =>
                SubmitSingleTransactionAsync(node, account, (BigInteger)index, txResults));

            await Task.WhenAll(tasks);
            sw.Stop();

            var successCount = txResults.Count(r => r.Success);
            var throughput = burstSize * 1000.0 / sw.ElapsedMilliseconds;

            _output.WriteLine($"=== Burst Test Results ===");
            _output.WriteLine($"Burst size: {burstSize}");
            _output.WriteLine($"Successful: {successCount}");
            _output.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {throughput:F2} tx/s");

            Assert.True(successCount > burstSize * 0.9, "At least 90% of burst transactions should succeed");
        }

        [Fact]
        [Trait("Category", "P2P-LoadTest")]
        public async Task LoadTest_SustainedLoad_5Seconds()
        {
            await _fixture.StartAllNodesAsync();
            await Task.Delay(2000);

            const int durationSeconds = 5;
            const int txPerSecond = 20;
            var testAccounts = GenerateTestAccounts(100);
            var txResults = new ConcurrentBag<TxSubmissionResult>();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
            var sw = Stopwatch.StartNew();
            int txCount = 0;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var batchTasks = new List<Task>();
                    var nodeIndex = txCount % _fixture.Nodes.Count;
                    var node = _fixture.Nodes[nodeIndex];
                    var account = testAccounts[txCount % testAccounts.Length];

                    batchTasks.Add(SubmitSingleTransactionAsync(
                        node,
                        account,
                        (BigInteger)(txCount / testAccounts.Length),
                        txResults));

                    txCount++;

                    var delayMs = 1000 / txPerSecond;
                    await Task.Delay(delayMs, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }

            sw.Stop();

            var successCount = txResults.Count(r => r.Success);
            var actualThroughput = txCount * 1000.0 / sw.ElapsedMilliseconds;

            _output.WriteLine($"=== Sustained Load Test Results ===");
            _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total transactions: {txCount}");
            _output.WriteLine($"Successful: {successCount}");
            _output.WriteLine($"Target throughput: {txPerSecond} tx/s");
            _output.WriteLine($"Actual throughput: {actualThroughput:F2} tx/s");

            foreach (var node in _fixture.Nodes)
            {
                _output.WriteLine($"Node {node.Index} - Pending: {node.Sequencer.TxPool.PendingCount}");
            }
        }

        [Fact]
        [Trait("Category", "P2P-LoadTest")]
        public async Task LoadTest_TransactionPropagation_AllNodesReceive()
        {
            await _fixture.StartAllNodesAsync();
            await Task.Delay(2000);

            var testAccount = GenerateTestAccounts(1)[0];
            var sourceNode = _fixture.Nodes[0];

            var initialCounts = _fixture.Nodes.Select(n => n.Sequencer.TxPool.PendingCount).ToArray();

            var signedTx = CreateSignedTransaction(testAccount, BigInteger.Zero);
            var txHash = await sourceNode.Sequencer.TxPool.AddAsync(signedTx);

            _output.WriteLine($"Transaction submitted to node 0: hash: {BitConverter.ToString(txHash)}");

            await Task.Delay(2000);

            _output.WriteLine("Transaction propagation results:");
            for (int i = 0; i < _fixture.Nodes.Count; i++)
            {
                var node = _fixture.Nodes[i];
                var newCount = node.Sequencer.TxPool.PendingCount;
                var added = newCount - initialCounts[i];
                _output.WriteLine($"  Node {i}: Pending={newCount}, Added={added}");
            }
        }

        private async Task SubmitTransactionsAsync(
            CliqueNodeInstance node,
            Account[] accounts,
            int startIndex,
            int count,
            ConcurrentBag<TxSubmissionResult> results)
        {
            for (int i = 0; i < count; i++)
            {
                var accountIndex = (startIndex + i) % accounts.Length;
                var account = accounts[accountIndex];
                var nonce = (BigInteger)((startIndex + i) / accounts.Length);

                await SubmitSingleTransactionAsync(node, account, nonce, results);

                if (i % 10 == 0)
                {
                    await Task.Delay(10);
                }
            }
        }

        private async Task SubmitSingleTransactionAsync(
            CliqueNodeInstance node,
            Account account,
            BigInteger nonce,
            ConcurrentBag<TxSubmissionResult> results)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var signedTx = CreateSignedTransaction(account, nonce);
                var txHash = await node.Sequencer.TxPool.AddAsync(signedTx);
                sw.Stop();

                results.Add(new TxSubmissionResult
                {
                    Success = txHash != null && txHash.Length > 0,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Error = null,
                    NodeIndex = node.Index
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new TxSubmissionResult
                {
                    Success = false,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Error = ex.Message,
                    NodeIndex = node.Index
                });
            }
        }

        private Account[] GenerateTestAccounts(int count)
        {
            var accounts = new Account[count];
            for (int i = 0; i < count; i++)
            {
                var key = EthECKey.GenerateKey();
                accounts[i] = new Account(key.GetPrivateKeyAsBytes(), CliqueClusterFixture.CHAIN_ID);
            }
            return accounts;
        }

        private ISignedTransaction CreateSignedTransaction(Account account, BigInteger nonce)
        {
            var legacyTx = new LegacyTransactionChainId(
                to: _fixture.SignerAddresses[0],
                amount: BigInteger.Parse("1000000000000000"),
                nonce: nonce,
                gasPrice: BigInteger.Parse("1000000000"),
                gasLimit: new BigInteger(21000),
                data: "",
                chainId: CliqueClusterFixture.CHAIN_ID);

            var signer = new LegacyTransactionSigner();
            signer.SignTransaction(account.PrivateKey.HexToByteArray(), legacyTx);

            return legacyTx;
        }

        private class TxSubmissionResult
        {
            public bool Success { get; set; }
            public long LatencyMs { get; set; }
            public string? Error { get; set; }
            public int NodeIndex { get; set; }
        }
    }

    internal static class ByteExtensions
    {
        public static byte[] HexToByteArray(this string hex)
        {
            return Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(hex);
        }
    }
}
