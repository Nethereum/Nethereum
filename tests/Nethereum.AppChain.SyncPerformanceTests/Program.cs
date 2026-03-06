using System.CommandLine;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Numerics;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.AppChain;
using Nethereum.AppChain.Sync;

using NethWeb3 = Nethereum.Web3.Web3;

namespace Nethereum.AppChain.SyncPerformanceTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var scenarioOption = new Option<string>("--scenario", "Test scenario to run") { IsRequired = true };
        scenarioOption.AddAlias("-s");

        var sequencerUrlOption = new Option<string>("--sequencer-url", () => "http://127.0.0.1:8545", "Sequencer RPC URL");
        sequencerUrlOption.AddAlias("-seq");

        var replicaUrlOption = new Option<string>("--replica-url", () => "http://127.0.0.1:8546", "Replica RPC URL");
        replicaUrlOption.AddAlias("-rep");

        var durationOption = new Option<int>("--duration", () => 60, "Test duration in seconds");
        durationOption.AddAlias("-d");

        var concurrencyOption = new Option<int>("--concurrency", () => 10, "Number of concurrent workers");
        concurrencyOption.AddAlias("-c");

        var batchSizeOption = new Option<int>("--batch-size", () => 100, "Blocks per batch for batch tests");
        var warmupOption = new Option<int>("--warmup", () => 5, "Warmup duration in seconds");
        var privateKeyOption = new Option<string?>("--key", "Private key for tx tests (optional)");

        var rootCommand = new RootCommand("AppChain Sync Performance Testing Tool")
        {
            scenarioOption,
            sequencerUrlOption,
            replicaUrlOption,
            durationOption,
            concurrencyOption,
            batchSizeOption,
            warmupOption,
            privateKeyOption
        };

        rootCommand.Description = @"
Available scenarios:
  live-sync         Test live block sync latency and throughput
  state-queries     Test state query latency (eth_getBalance, eth_getCode)
  storage-queries   Test storage read performance (eth_getStorageAt, eth_getProof)
  log-queries       Test log filtering performance (eth_getLogs)
  concurrent-reads  Test concurrent read scalability
  rpc-throughput    Test overall RPC throughput (mixed reads)
  tx-forwarding     Test transaction forwarding latency (replica -> sequencer)
  full-sync-e2e     Full end-to-end sync test";

        rootCommand.SetHandler(async (context) =>
        {
            var config = new SyncPerfConfig
            {
                Scenario = context.ParseResult.GetValueForOption(scenarioOption)!,
                SequencerUrl = context.ParseResult.GetValueForOption(sequencerUrlOption)!,
                ReplicaUrl = context.ParseResult.GetValueForOption(replicaUrlOption)!,
                DurationSeconds = context.ParseResult.GetValueForOption(durationOption),
                Concurrency = context.ParseResult.GetValueForOption(concurrencyOption),
                BatchSize = context.ParseResult.GetValueForOption(batchSizeOption),
                WarmupSeconds = context.ParseResult.GetValueForOption(warmupOption),
                PrivateKey = context.ParseResult.GetValueForOption(privateKeyOption)
            };

            await RunScenarioAsync(config);
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunScenarioAsync(SyncPerfConfig config)
    {
        PrintHeader(config);

        switch (config.Scenario.ToLower())
        {
            case "live-sync":
                await RunLiveSyncTestAsync(config);
                break;
            case "state-queries":
                await RunStateQueriesTestAsync(config);
                break;
            case "storage-queries":
                await RunStorageQueriesTestAsync(config);
                break;
            case "log-queries":
                await RunLogQueriesTestAsync(config);
                break;
            case "concurrent-reads":
                await RunConcurrentReadsTestAsync(config);
                break;
            case "rpc-throughput":
                await RunRpcThroughputTestAsync(config);
                break;
            case "tx-forwarding":
                await RunTxForwardingTestAsync(config);
                break;
            case "full-sync-e2e":
                await RunFullSyncE2ETestAsync(config);
                break;
            default:
                Console.WriteLine($"Unknown scenario: {config.Scenario}");
                Console.WriteLine("Use --help to see available scenarios");
                break;
        }
    }

    private static void PrintHeader(SyncPerfConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           AppChain Sync Performance Test                      ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Scenario:       {config.Scenario,-47} ║");
        Console.WriteLine($"║ Sequencer:      {config.SequencerUrl,-47} ║");
        Console.WriteLine($"║ Replica:        {config.ReplicaUrl,-47} ║");
        Console.WriteLine($"║ Duration:       {config.DurationSeconds}s{"",-44} ║");
        Console.WriteLine($"║ Concurrency:    {config.Concurrency,-47} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static async Task RunLiveSyncTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Testing live sync latency between sequencer and replica...");

        var sequencerWeb3 = new NethWeb3(config.SequencerUrl);
        var replicaWeb3 = new NethWeb3(config.ReplicaUrl);

        var metrics = new SyncLagMetrics();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.DurationSeconds));

        var reporterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                var stats = metrics.GetStats();
                Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ");
                Console.Write($"Lag: {stats.CurrentLag} blocks | ");
                Console.Write($"Avg: {stats.AvgLag:F1} | ");
                Console.Write($"Max: {stats.MaxLag} | ");
                Console.Write($"Samples: {stats.SampleCount}     ");
            }
        });

        var pollTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var sequencerBlock = await sequencerWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    var replicaBlock = await replicaWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                    var lag = (long)(sequencerBlock.Value - replicaBlock.Value);
                    metrics.RecordLag(lag);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError polling: {ex.Message}");
                }

                await Task.Delay(100, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        });

        await Task.WhenAll(pollTask, reporterTask);

        Console.WriteLine();
        PrintLiveSyncResults(metrics);
    }

    private static async Task RunStateQueriesTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Testing state query latency (eth_getBalance, eth_getCode, eth_getTransactionCount)...");

        var metrics = new QueryMetrics();
        await RunQueryWorkersAsync(config, metrics, async (web3, random, metrics, ct) =>
        {
            var queryType = random.Next(3);
            var address = GenerateRandomAddress(random);

            switch (queryType)
            {
                case 0:
                    await web3.Eth.GetBalance.SendRequestAsync(address);
                    return "eth_getBalance";
                case 1:
                    await web3.Eth.GetCode.SendRequestAsync(address);
                    return "eth_getCode";
                default:
                    await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(address);
                    return "eth_getTransactionCount";
            }
        });

        PrintQueryResults(metrics);
    }

    private static async Task RunStorageQueriesTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Testing storage query latency (eth_getStorageAt, eth_getProof)...");

        var metrics = new QueryMetrics();
        await RunQueryWorkersAsync(config, metrics, async (web3, random, metrics, ct) =>
        {
            var queryType = random.Next(2);
            var address = "0x4e59b44847b379578588920cA78FbF26c0B4956C"; // CREATE2 factory
            var slot = random.Next(0, 100);

            switch (queryType)
            {
                case 0:
                    await web3.Eth.GetStorageAt.SendRequestAsync(address, new HexBigInteger(slot));
                    return "eth_getStorageAt";
                default:
                    try
                    {
                        await web3.Eth.GetProof.SendRequestAsync(address,
                            new[] { "0x" + slot.ToString("x") },
                            BlockParameter.CreateLatest());
                        return "eth_getProof";
                    }
                    catch
                    {
                        await web3.Eth.GetStorageAt.SendRequestAsync(address, new HexBigInteger(slot));
                        return "eth_getStorageAt";
                    }
            }
        });

        PrintQueryResults(metrics);
    }

    private static async Task RunLogQueriesTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Testing log query latency (eth_getLogs)...");

        var metrics = new QueryMetrics();
        var replicaWeb3 = new NethWeb3(config.ReplicaUrl);

        var latestBlock = await replicaWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        var maxBlock = (long)latestBlock.Value;

        await RunQueryWorkersAsync(config, metrics, async (web3, random, metrics, ct) =>
        {
            var queryType = random.Next(3);
            var fromBlock = (ulong)Math.Max(0, maxBlock - random.Next(100, 1000));
            var toBlock = (ulong)Math.Min(maxBlock, (long)fromBlock + random.Next(10, 100));

            var filter = new NewFilterInput
            {
                FromBlock = new BlockParameter(fromBlock),
                ToBlock = new BlockParameter(toBlock)
            };

            if (queryType == 1)
            {
                filter.Address = new[] { GenerateRandomAddress(random) };
            }
            else if (queryType == 2)
            {
                filter.Topics = new object[] { "0x" + Guid.NewGuid().ToString("N") };
            }

            await web3.Eth.Filters.GetLogs.SendRequestAsync(filter);
            return queryType switch
            {
                0 => "eth_getLogs_range",
                1 => "eth_getLogs_address",
                _ => "eth_getLogs_topic"
            };
        });

        PrintQueryResults(metrics);
    }

    private static async Task RunConcurrentReadsTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine($"Testing concurrent read scalability with {config.Concurrency} workers...");

        var metrics = new QueryMetrics();
        await RunQueryWorkersAsync(config, metrics, async (web3, random, metrics, ct) =>
        {
            var queryType = random.Next(5);

            switch (queryType)
            {
                case 0:
                    await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    return "eth_blockNumber";
                case 1:
                    await web3.Eth.GetBalance.SendRequestAsync(GenerateRandomAddress(random));
                    return "eth_getBalance";
                case 2:
                    var blockNum = (ulong)random.Next(0, 100);
                    await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter(blockNum));
                    return "eth_getBlockByNumber";
                case 3:
                    await web3.Eth.GetStorageAt.SendRequestAsync(
                        GenerateRandomAddress(random),
                        new HexBigInteger(random.Next(0, 10)));
                    return "eth_getStorageAt";
                default:
                    await web3.Eth.ChainId.SendRequestAsync();
                    return "eth_chainId";
            }
        });

        PrintQueryResults(metrics);
    }

    private static async Task RunRpcThroughputTestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Testing overall RPC throughput with mixed read workload...");

        var metrics = new QueryMetrics();
        await RunQueryWorkersAsync(config, metrics, async (web3, random, metrics, ct) =>
        {
            var queryType = random.Next(10);

            switch (queryType)
            {
                case 0:
                case 1:
                case 2:
                    await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    return "eth_blockNumber";
                case 3:
                case 4:
                    await web3.Eth.GetBalance.SendRequestAsync(GenerateRandomAddress(random));
                    return "eth_getBalance";
                case 5:
                    await web3.Eth.ChainId.SendRequestAsync();
                    return "eth_chainId";
                case 6:
                    await web3.Eth.GasPrice.SendRequestAsync();
                    return "eth_gasPrice";
                case 7:
                    await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter((ulong)random.Next(0, 100)));
                    return "eth_getBlockByNumber";
                case 8:
                    await web3.Eth.GetCode.SendRequestAsync(GenerateRandomAddress(random));
                    return "eth_getCode";
                default:
                    await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(GenerateRandomAddress(random));
                    return "eth_getTransactionCount";
            }
        });

        PrintQueryResults(metrics);
    }

    private static async Task RunTxForwardingTestAsync(SyncPerfConfig config)
    {
        if (string.IsNullOrEmpty(config.PrivateKey))
        {
            Console.WriteLine("ERROR: --key is required for tx-forwarding scenario");
            return;
        }

        Console.WriteLine("Testing transaction forwarding latency (replica -> sequencer)...");

        EthECKey.SignRecoverable = true;
        var masterKey = new EthECKey(config.PrivateKey);
        var masterAddress = masterKey.GetPublicAddress();
        Console.WriteLine($"Master account: {masterAddress}");

        var accounts = new List<Account>();
        Console.WriteLine($"Generating {config.Concurrency} test accounts...");
        for (int i = 0; i < config.Concurrency; i++)
        {
            var key = EthECKey.GenerateKey();
            accounts.Add(new Account(key.GetPrivateKey(), 420420));
        }

        Console.WriteLine("Funding test accounts via sequencer...");
        var sequencerWeb3 = new NethWeb3(new Account(config.PrivateKey, 420420), config.SequencerUrl);
        sequencerWeb3.TransactionManager.UseLegacyAsDefault = true;

        foreach (var acc in accounts)
        {
            await sequencerWeb3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(acc.Address, 10m);
        }
        Console.WriteLine("Accounts funded.");

        if (config.WarmupSeconds > 0)
        {
            Console.WriteLine($"Warming up for {config.WarmupSeconds} seconds...");
            await Task.Delay(config.WarmupSeconds * 1000);
        }

        var metrics = new TxForwardingMetrics();
        var cts = new CancellationTokenSource();

        Console.WriteLine();
        Console.WriteLine("Starting tx forwarding test (sending via replica)...");
        Console.WriteLine("Press Ctrl+C to stop early");
        Console.WriteLine();

        var reporterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                PrintTxMetrics(metrics);
            }
        });

        var workers = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < config.Concurrency; i++)
        {
            var account = accounts[i];
            var worker = RunTxWorkerAsync(config, account, metrics, cts.Token);
            workers.Add(worker);
        }

        await Task.Delay(config.DurationSeconds * 1000);
        cts.Cancel();

        await Task.WhenAll(workers);
        stopwatch.Stop();

        Console.WriteLine();
        PrintTxFinalResults(metrics, stopwatch.Elapsed);
    }

    private static async Task RunTxWorkerAsync(
        SyncPerfConfig config,
        Account account,
        TxForwardingMetrics metrics,
        CancellationToken ct)
    {
        var web3 = new NethWeb3(account, config.ReplicaUrl);
        web3.TransactionManager.UseLegacyAsDefault = true;
        var targetAddress = "0x0000000000000000000000000000000000000001";

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var receipt = await web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync(targetAddress, 0.001m);
                sw.Stop();

                if (receipt?.Succeeded() == true)
                {
                    metrics.RecordSuccess(sw.ElapsedMilliseconds);
                }
                else
                {
                    metrics.RecordFailure(sw.ElapsedMilliseconds, "TX failed");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordFailure(sw.ElapsedMilliseconds, ex.Message);
            }
        }
    }

    private static async Task RunFullSyncE2ETestAsync(SyncPerfConfig config)
    {
        Console.WriteLine("Running full end-to-end sync test...");
        Console.WriteLine();

        Console.WriteLine("Phase 1: Testing sequencer connectivity...");
        var sequencerWeb3 = new NethWeb3(config.SequencerUrl);
        try
        {
            var seqBlock = await sequencerWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var seqChainId = await sequencerWeb3.Eth.ChainId.SendRequestAsync();
            Console.WriteLine($"  Sequencer block: {seqBlock.Value}");
            Console.WriteLine($"  Chain ID: {seqChainId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: Cannot connect to sequencer: {ex.Message}");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Phase 2: Testing replica connectivity...");
        var replicaWeb3 = new NethWeb3(config.ReplicaUrl);
        try
        {
            var repBlock = await replicaWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var repChainId = await replicaWeb3.Eth.ChainId.SendRequestAsync();
            Console.WriteLine($"  Replica block: {repBlock.Value}");
            Console.WriteLine($"  Chain ID: {repChainId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: Cannot connect to replica: {ex.Message}");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Phase 3: Testing replica sync status...");
        try
        {
            var syncStatus = await replicaWeb3.Client.SendRequestAsync<ReplicaSyncStatusDto>(
                new Nethereum.JsonRpc.Client.RpcRequest(
                    Guid.NewGuid().ToString(),
                    "replica_syncStatus"));

            if (syncStatus != null)
            {
                Console.WriteLine($"  Is Replica: {syncStatus.IsReplica}");
                Console.WriteLine($"  Syncing: {syncStatus.Syncing}");
                Console.WriteLine($"  Sync Mode: {syncStatus.SyncMode}");
                Console.WriteLine($"  Finalized Block: {syncStatus.FinalizedBlock}");
                Console.WriteLine($"  Soft Block: {syncStatus.SoftBlock}");
                Console.WriteLine($"  Anchored Block: {syncStatus.AnchoredBlock}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  replica_syncStatus not available: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Phase 4: Running live sync lag test (10s)...");
        var liveSyncConfig = config with { DurationSeconds = 10 };
        await RunLiveSyncTestAsync(liveSyncConfig);

        Console.WriteLine();
        Console.WriteLine("Phase 5: Running state query test (10s)...");
        var stateQueryConfig = config with { DurationSeconds = 10 };
        await RunStateQueriesTestAsync(stateQueryConfig);

        Console.WriteLine();
        Console.WriteLine("Phase 6: Running RPC throughput test (10s)...");
        var rpcConfig = config with { DurationSeconds = 10 };
        await RunRpcThroughputTestAsync(rpcConfig);

        if (!string.IsNullOrEmpty(config.PrivateKey))
        {
            Console.WriteLine();
            Console.WriteLine("Phase 7: Running tx forwarding test (10s)...");
            var txConfig = config with { DurationSeconds = 10 };
            await RunTxForwardingTestAsync(txConfig);
        }

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Full E2E Sync Test Complete                         ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }

    private static async Task RunQueryWorkersAsync(
        SyncPerfConfig config,
        QueryMetrics metrics,
        Func<NethWeb3, Random, QueryMetrics, CancellationToken, Task<string>> queryFunc)
    {
        if (config.WarmupSeconds > 0)
        {
            Console.WriteLine($"Warming up for {config.WarmupSeconds} seconds...");
            await Task.Delay(config.WarmupSeconds * 1000);
        }

        var cts = new CancellationTokenSource();

        var reporterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                PrintQueryMetrics(metrics);
            }
        });

        var workers = new List<Task>();
        for (int i = 0; i < config.Concurrency; i++)
        {
            var worker = RunQueryWorkerAsync(config.ReplicaUrl, metrics, queryFunc, cts.Token);
            workers.Add(worker);
        }

        await Task.Delay(config.DurationSeconds * 1000);
        cts.Cancel();
        await Task.WhenAll(workers);
        Console.WriteLine();
    }

    private static async Task RunQueryWorkerAsync(
        string rpcUrl,
        QueryMetrics metrics,
        Func<NethWeb3, Random, QueryMetrics, CancellationToken, Task<string>> queryFunc,
        CancellationToken ct)
    {
        var web3 = new NethWeb3(rpcUrl);
        var random = new Random();

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            string? queryType = null;

            try
            {
                queryType = await queryFunc(web3, random, metrics, ct);
                sw.Stop();
                metrics.RecordSuccess(queryType, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordFailure(queryType ?? "unknown", sw.ElapsedMilliseconds, ex.Message);
            }
        }
    }

    private static string GenerateRandomAddress(Random random)
    {
        var bytes = new byte[20];
        random.NextBytes(bytes);
        return "0x" + bytes.ToHex();
    }

    private static void PrintQueryMetrics(QueryMetrics metrics)
    {
        var stats = metrics.GetStats();
        Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ");
        Console.Write($"QPS: {stats.CurrentQps,6:F1} | ");
        Console.Write($"Total: {stats.TotalSuccess,6} | ");
        Console.Write($"Failed: {stats.TotalFailed,4} | ");
        Console.Write($"Avg: {stats.AvgLatencyMs,6:F1}ms | ");
        Console.Write($"P99: {stats.P99LatencyMs,6:F1}ms");
    }

    private static void PrintQueryResults(QueryMetrics metrics)
    {
        var stats = metrics.GetStats();
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    QUERY RESULTS                              ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Total Queries:  {stats.TotalSuccess + stats.TotalFailed,-47} ║");
        Console.WriteLine($"║ Successful:     {stats.TotalSuccess,-47} ║");
        Console.WriteLine($"║ Failed:         {stats.TotalFailed,-47} ║");
        Console.WriteLine($"║ Peak QPS:       {stats.PeakQps,-47:F2} ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Avg Latency:    {stats.AvgLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ Min Latency:    {stats.MinLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ Max Latency:    {stats.MaxLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P50 Latency:    {stats.P50LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P95 Latency:    {stats.P95LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P99 Latency:    {stats.P99LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ By Query Type:                                                ║");
        foreach (var kvp in stats.ByQueryType.OrderByDescending(x => x.Value.Count))
        {
            var name = kvp.Key.Length > 25 ? kvp.Key.Substring(0, 25) : kvp.Key;
            Console.WriteLine($"║   {name,-25} {kvp.Value.Count,6} calls, avg {kvp.Value.AvgMs,6:F1}ms ║");
        }
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }

    private static void PrintLiveSyncResults(SyncLagMetrics metrics)
    {
        var stats = metrics.GetStats();
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 LIVE SYNC RESULTS                             ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Samples:        {stats.SampleCount,-47} ║");
        Console.WriteLine($"║ Average Lag:    {stats.AvgLag:F2} blocks{"",-34} ║");
        Console.WriteLine($"║ Min Lag:        {stats.MinLag} blocks{"",-36} ║");
        Console.WriteLine($"║ Max Lag:        {stats.MaxLag} blocks{"",-36} ║");
        Console.WriteLine($"║ P95 Lag:        {stats.P95Lag} blocks{"",-36} ║");
        Console.WriteLine($"║ At Zero:        {stats.AtZeroPercent:F1}% of samples{"",-28} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }

    private static void PrintTxMetrics(TxForwardingMetrics metrics)
    {
        var stats = metrics.GetStats();
        Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ");
        Console.Write($"TPS: {stats.CurrentTps,6:F1} | ");
        Console.Write($"Total: {stats.TotalSuccess,6} | ");
        Console.Write($"Failed: {stats.TotalFailed,4} | ");
        Console.Write($"Avg: {stats.AvgLatencyMs,6:F1}ms | ");
        Console.Write($"P99: {stats.P99LatencyMs,6:F1}ms");
    }

    private static void PrintTxFinalResults(TxForwardingMetrics metrics, TimeSpan elapsed)
    {
        var stats = metrics.GetStats();
        var totalTps = stats.TotalSuccess / elapsed.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║             TX FORWARDING RESULTS                             ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Duration:       {elapsed.TotalSeconds:F1}s{"",-43} ║");
        Console.WriteLine($"║ Total TX:       {stats.TotalSuccess + stats.TotalFailed,-47} ║");
        Console.WriteLine($"║ Successful:     {stats.TotalSuccess,-47} ║");
        Console.WriteLine($"║ Failed:         {stats.TotalFailed,-47} ║");
        Console.WriteLine($"║ Average TPS:    {totalTps,-47:F2} ║");
        Console.WriteLine($"║ Peak TPS:       {stats.PeakTps,-47:F2} ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Avg Latency:    {stats.AvgLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ Min Latency:    {stats.MinLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ Max Latency:    {stats.MaxLatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P50 Latency:    {stats.P50LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P95 Latency:    {stats.P95LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine($"║ P99 Latency:    {stats.P99LatencyMs:F2}ms{"",-40} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }
}

public record SyncPerfConfig
{
    public string Scenario { get; init; } = "";
    public string SequencerUrl { get; init; } = "";
    public string ReplicaUrl { get; init; } = "";
    public int DurationSeconds { get; init; } = 60;
    public int Concurrency { get; init; } = 10;
    public int BatchSize { get; init; } = 100;
    public int WarmupSeconds { get; init; } = 5;
    public string? PrivateKey { get; init; }
}

public class ReplicaSyncStatusDto
{
    public bool IsReplica { get; set; }
    public bool Syncing { get; set; }
    public string SyncMode { get; set; } = "";
    public string FinalizedBlock { get; set; } = "0";
    public string SoftBlock { get; set; } = "0";
    public string AnchoredBlock { get; set; } = "0";
}

public class SyncLagMetrics
{
    private readonly ConcurrentBag<long> _lags = new();
    private long _currentLag;

    public void RecordLag(long lag)
    {
        Interlocked.Exchange(ref _currentLag, lag);
        _lags.Add(lag);
    }

    public SyncLagStats GetStats()
    {
        var lags = _lags.ToArray();
        Array.Sort(lags);

        var atZero = lags.Count(l => l == 0);

        return new SyncLagStats
        {
            SampleCount = lags.Length,
            CurrentLag = _currentLag,
            AvgLag = lags.Length > 0 ? lags.Average() : 0,
            MinLag = lags.Length > 0 ? lags.Min() : 0,
            MaxLag = lags.Length > 0 ? lags.Max() : 0,
            P95Lag = GetPercentile(lags, 95),
            AtZeroPercent = lags.Length > 0 ? (atZero * 100.0 / lags.Length) : 0
        };
    }

    private static long GetPercentile(long[] sorted, int percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

public class SyncLagStats
{
    public int SampleCount { get; set; }
    public long CurrentLag { get; set; }
    public double AvgLag { get; set; }
    public long MinLag { get; set; }
    public long MaxLag { get; set; }
    public long P95Lag { get; set; }
    public double AtZeroPercent { get; set; }
}

public class QueryMetrics
{
    private long _totalSuccess;
    private long _totalFailed;
    private long _lastSecondCount;
    private long _lastSecondTime;
    private double _peakQps;
    private readonly ConcurrentBag<long> _latencies = new();
    private readonly ConcurrentDictionary<string, QueryTypeStats> _byType = new();
    private readonly object _lock = new();

    public void RecordSuccess(string queryType, long latencyMs)
    {
        Interlocked.Increment(ref _totalSuccess);
        _latencies.Add(latencyMs);
        RecordByType(queryType, latencyMs, true);
        UpdateQps();
    }

    public void RecordFailure(string queryType, long latencyMs, string error)
    {
        Interlocked.Increment(ref _totalFailed);
        _latencies.Add(latencyMs);
        RecordByType(queryType, latencyMs, false);
        UpdateQps();
    }

    private void RecordByType(string queryType, long latencyMs, bool success)
    {
        _byType.AddOrUpdate(queryType,
            _ => new QueryTypeStats { Count = 1, TotalMs = latencyMs, Failures = success ? 0 : 1 },
            (_, existing) =>
            {
                Interlocked.Increment(ref existing.Count);
                Interlocked.Add(ref existing.TotalMs, latencyMs);
                if (!success) Interlocked.Increment(ref existing.Failures);
                return existing;
            });
    }

    private void UpdateQps()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_lock)
        {
            if (now - _lastSecondTime >= 1000)
            {
                var count = _totalSuccess - _lastSecondCount;
                var elapsed = (now - _lastSecondTime) / 1000.0;
                var qps = count / elapsed;
                if (qps > _peakQps) _peakQps = qps;
                _lastSecondCount = _totalSuccess;
                _lastSecondTime = now;
            }
        }
    }

    public QueryStats GetStats()
    {
        var latencies = _latencies.ToArray();
        Array.Sort(latencies);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var currentQps = 0.0;
        lock (_lock)
        {
            if (now - _lastSecondTime < 2000 && now - _lastSecondTime > 0)
            {
                currentQps = (_totalSuccess - _lastSecondCount) / ((now - _lastSecondTime) / 1000.0);
            }
        }

        var byType = _byType.ToDictionary(
            kvp => kvp.Key,
            kvp => new QueryTypeSummary
            {
                Count = kvp.Value.Count,
                AvgMs = kvp.Value.Count > 0 ? (double)kvp.Value.TotalMs / kvp.Value.Count : 0,
                Failures = kvp.Value.Failures
            });

        return new QueryStats
        {
            TotalSuccess = _totalSuccess,
            TotalFailed = _totalFailed,
            CurrentQps = currentQps,
            PeakQps = _peakQps,
            AvgLatencyMs = latencies.Length > 0 ? latencies.Average() : 0,
            MinLatencyMs = latencies.Length > 0 ? latencies.Min() : 0,
            MaxLatencyMs = latencies.Length > 0 ? latencies.Max() : 0,
            P50LatencyMs = GetPercentile(latencies, 50),
            P95LatencyMs = GetPercentile(latencies, 95),
            P99LatencyMs = GetPercentile(latencies, 99),
            ByQueryType = byType
        };
    }

    private static double GetPercentile(long[] sorted, int percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

public class QueryTypeStats
{
    public long Count;
    public long TotalMs;
    public long Failures;
}

public class QueryTypeSummary
{
    public long Count { get; set; }
    public double AvgMs { get; set; }
    public long Failures { get; set; }
}

public class QueryStats
{
    public long TotalSuccess { get; set; }
    public long TotalFailed { get; set; }
    public double CurrentQps { get; set; }
    public double PeakQps { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public Dictionary<string, QueryTypeSummary> ByQueryType { get; set; } = new();
}

public class TxForwardingMetrics
{
    private long _totalSuccess;
    private long _totalFailed;
    private long _lastSecondCount;
    private long _lastSecondTime;
    private double _peakTps;
    private readonly ConcurrentBag<long> _latencies = new();
    private readonly object _lock = new();

    public void RecordSuccess(long latencyMs)
    {
        Interlocked.Increment(ref _totalSuccess);
        _latencies.Add(latencyMs);
        UpdateTps();
    }

    public void RecordFailure(long latencyMs, string error)
    {
        Interlocked.Increment(ref _totalFailed);
        _latencies.Add(latencyMs);
        UpdateTps();
    }

    private void UpdateTps()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_lock)
        {
            if (now - _lastSecondTime >= 1000)
            {
                var count = _totalSuccess - _lastSecondCount;
                var elapsed = (now - _lastSecondTime) / 1000.0;
                var tps = count / elapsed;
                if (tps > _peakTps) _peakTps = tps;
                _lastSecondCount = _totalSuccess;
                _lastSecondTime = now;
            }
        }
    }

    public TxStats GetStats()
    {
        var latencies = _latencies.ToArray();
        Array.Sort(latencies);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var currentTps = 0.0;
        lock (_lock)
        {
            if (now - _lastSecondTime < 2000 && now - _lastSecondTime > 0)
            {
                currentTps = (_totalSuccess - _lastSecondCount) / ((now - _lastSecondTime) / 1000.0);
            }
        }

        return new TxStats
        {
            TotalSuccess = _totalSuccess,
            TotalFailed = _totalFailed,
            CurrentTps = currentTps,
            PeakTps = _peakTps,
            AvgLatencyMs = latencies.Length > 0 ? latencies.Average() : 0,
            MinLatencyMs = latencies.Length > 0 ? latencies.Min() : 0,
            MaxLatencyMs = latencies.Length > 0 ? latencies.Max() : 0,
            P50LatencyMs = GetPercentile(latencies, 50),
            P95LatencyMs = GetPercentile(latencies, 95),
            P99LatencyMs = GetPercentile(latencies, 99)
        };
    }

    private static double GetPercentile(long[] sorted, int percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

public class TxStats
{
    public long TotalSuccess { get; set; }
    public long TotalFailed { get; set; }
    public double CurrentTps { get; set; }
    public double PeakTps { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
}
