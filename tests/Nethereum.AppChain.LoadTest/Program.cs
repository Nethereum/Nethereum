using System.CommandLine;
using System.Diagnostics;
using System.Numerics;
using System.Collections.Concurrent;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.NonceServices;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI;
using Nethereum.Merkle.Patricia;
using Nethereum.RPC.Eth.Mappers;

using NethWeb3 = Nethereum.Web3.Web3;

namespace Nethereum.AppChain.LoadTest;

public class Program
{
    // Shared state for complex tests
    private static string? _benchmarkContractAddress;
    private static string? _erc20ContractAddress;
    private static List<string>? _testAccountAddresses;
    private static BigInteger _setupBlockStart;
    private static BigInteger _setupBlockEnd;
    private static int _storageSlotCount;
    private static int _eventCount;

    // Async receipt collection
    private static readonly ConcurrentQueue<(string TxHash, long SendTimeMs, Stopwatch Sw)> _pendingReceipts = new();
    private static long _confirmedCount;
    private static long _failedConfirmCount;
    private static long _revertedCount;
    private static long _missingLogsCount;

    // ERC20 Transfer event topic: keccak256("Transfer(address,address,uint256)")
    private const string ERC20_TRANSFER_TOPIC = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    public static async Task<int> Main(string[] args)
    {
        var rpcUrlOption = new Option<string>("--rpc", () => "http://127.0.0.1:8546", "RPC URL");
        var privateKeyOption = new Option<string>("--key", "Private key for sending transactions") { IsRequired = true };
        var concurrencyOption = new Option<int>("--concurrency", () => 10, "Number of concurrent senders");
        var durationOption = new Option<int>("--duration", () => 30, "Test duration in seconds");
        var txTypeOption = new Option<string>("--type", () => "transfer", "Test type: transfer, transfer-async, call, noop, logs, receipt, block, deploy, estimategas, proof, proof-verify, proof-scale, trace, storage-setup, storage-read, logs-setup, logs-range, erc20-transfer, erc20-async, erc20-balance");
        var slotCountOption = new Option<int>("--slots", () => 100, "Number of storage slots for storage tests");
        var eventCountOption = new Option<int>("--events", () => 50, "Number of events per tx for logs tests");
        var contractOption = new Option<string?>("--contract", "Existing benchmark contract address (skip deployment)");
        var targetOption = new Option<string?>("--target", "Target address for calls");
        var warmupOption = new Option<int>("--warmup", () => 5, "Warmup duration in seconds");

        var rootCommand = new RootCommand("AppChain Load Testing Tool")
        {
            rpcUrlOption,
            privateKeyOption,
            concurrencyOption,
            durationOption,
            txTypeOption,
            slotCountOption,
            eventCountOption,
            contractOption,
            targetOption,
            warmupOption
        };

        rootCommand.SetHandler(async (context) =>
        {
            var config = new LoadTestConfig
            {
                RpcUrl = context.ParseResult.GetValueForOption(rpcUrlOption)!,
                PrivateKey = context.ParseResult.GetValueForOption(privateKeyOption)!,
                Concurrency = context.ParseResult.GetValueForOption(concurrencyOption),
                DurationSeconds = context.ParseResult.GetValueForOption(durationOption),
                TxType = context.ParseResult.GetValueForOption(txTypeOption)!,
                SlotCount = context.ParseResult.GetValueForOption(slotCountOption),
                EventCount = context.ParseResult.GetValueForOption(eventCountOption),
                ContractAddress = context.ParseResult.GetValueForOption(contractOption),
                TargetAddress = context.ParseResult.GetValueForOption(targetOption),
                WarmupSeconds = context.ParseResult.GetValueForOption(warmupOption)
            };

            await RunLoadTestAsync(config);
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunLoadTestAsync(LoadTestConfig config)
    {
        // Enable native secp256k1
        EthECKey.SignRecoverable = true;

        if (config.TxType == "proof-scale")
        {
            await RunProofScaleBenchmarkAsync(config);
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              AppChain Load Test                               ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ RPC URL:        {config.RpcUrl,-47} ║");
        Console.WriteLine($"║ Concurrency:    {config.Concurrency,-47} ║");
        Console.WriteLine($"║ Duration:       {config.DurationSeconds}s{"",-44} ║");
        Console.WriteLine($"║ TX Type:        {config.TxType,-47} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var masterKey = new EthECKey(config.PrivateKey);
        var masterAddress = masterKey.GetPublicAddress();
        Console.WriteLine($"Master account: {masterAddress}");

        // Generate accounts for concurrent senders
        var accounts = new List<Account>();
        var accountKeys = new List<string>();

        Console.WriteLine($"Generating {config.Concurrency} test accounts...");
        for (int i = 0; i < config.Concurrency; i++)
        {
            var key = EthECKey.GenerateKey();
            accountKeys.Add(key.GetPrivateKey());
            accounts.Add(new Account(key.GetPrivateKey(), 420420));
        }

        // Fund accounts from master (batch send, then validate)
        Console.WriteLine("Funding test accounts...");
        var masterAccount = new Account(config.PrivateKey, 420420);
        var masterWeb3 = new NethWeb3(masterAccount, config.RpcUrl);
        masterWeb3.TransactionManager.UseLegacyAsDefault = true;

        var fundAmount = UnitConversion.Convert.ToWei(10); // 10 ETH each
        var fundTxHashes = new List<string>();

        // Send all funding TXs without waiting
        foreach (var acc in accounts)
        {
            var txHash = await masterWeb3.Eth.GetEtherTransferService()
                .TransferEtherAsync(acc.Address, 10m);
            fundTxHashes.Add(txHash);
        }
        Console.WriteLine($"Sent {fundTxHashes.Count} funding TXs, waiting for confirmation...");

        // Wait for last TX receipt (all previous should be confirmed by then)
        var lastTxHash = fundTxHashes.Last();
        var receiptService = masterWeb3.Eth.Transactions.GetTransactionReceipt;
        TransactionReceipt lastReceipt = null;
        var maxWait = TimeSpan.FromSeconds(30);
        var startWait = DateTime.UtcNow;
        while (lastReceipt == null && DateTime.UtcNow - startWait < maxWait)
        {
            lastReceipt = await receiptService.SendRequestAsync(lastTxHash);
            if (lastReceipt == null)
                await Task.Delay(100);
        }

        // Validate all balances
        var failedFunding = 0;
        foreach (var acc in accounts)
        {
            var balance = await masterWeb3.Eth.GetBalance.SendRequestAsync(acc.Address);
            if (balance.Value < fundAmount)
                failedFunding++;
        }

        if (failedFunding > 0)
            Console.WriteLine($"WARNING: {failedFunding}/{accounts.Count} accounts have insufficient balance");
        else
            Console.WriteLine("Accounts funded and validated.");

        // Store account addresses for ERC20 tests
        _testAccountAddresses = accounts.Select(a => a.Address).ToList();

        // Setup benchmark contract for complex tests
        if (config.TxType.StartsWith("storage-") || config.TxType.StartsWith("logs-"))
        {
            await SetupBenchmarkContractAsync(config, masterWeb3);
        }

        // Setup ERC20 contract for ERC20 tests
        if (config.TxType.StartsWith("erc20-"))
        {
            await SetupERC20ContractAsync(config, masterWeb3, accounts);
        }

        // Reset async counters
        _confirmedCount = 0;
        _failedConfirmCount = 0;
        _revertedCount = 0;
        _missingLogsCount = 0;
        while (_pendingReceipts.TryDequeue(out _)) { } // Clear queue

        // Warmup
        if (config.WarmupSeconds > 0)
        {
            Console.WriteLine($"Warming up for {config.WarmupSeconds} seconds...");
            await Task.Delay(config.WarmupSeconds * 1000);
        }

        // Metrics
        var metrics = new LoadTestMetrics();
        var cts = new CancellationTokenSource();

        Console.WriteLine();
        Console.WriteLine("Starting load test...");
        Console.WriteLine("Press Ctrl+C to stop early");
        Console.WriteLine();

        // Start metrics reporter
        var reporterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                PrintMetrics(metrics);
            }
        });

        // Start workers
        var workers = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Start receipt collector for async tests
        Task? receiptCollector = null;
        var isAsyncTest = config.TxType.EndsWith("-async");
        if (isAsyncTest)
        {
            receiptCollector = RunReceiptCollectorAsync(config.RpcUrl, metrics, cts.Token);
        }

        for (int i = 0; i < config.Concurrency; i++)
        {
            var account = accounts[i];
            var worker = RunWorkerAsync(config, account, metrics, cts.Token);
            workers.Add(worker);
        }

        // Wait for duration
        await Task.Delay(config.DurationSeconds * 1000);
        cts.Cancel();

        await Task.WhenAll(workers);

        // Wait for receipt collector to finish processing remaining receipts
        if (receiptCollector != null)
        {
            Console.WriteLine("\nWaiting for receipt confirmation...");
            await Task.Delay(2000); // Give time for final receipts
        }

        stopwatch.Stop();

        // Final report
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    FINAL RESULTS                              ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        PrintFinalMetrics(metrics, stopwatch.Elapsed);
        if (isAsyncTest)
        {
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Receipt Validation:                                           ║");
            Console.WriteLine($"║   Confirmed:      {Interlocked.Read(ref _confirmedCount),-45} ║");
            Console.WriteLine($"║   Unconfirmed:    {_pendingReceipts.Count,-45} ║");
            Console.WriteLine($"║   Reverted:       {Interlocked.Read(ref _revertedCount),-45} ║");
            if (config.TxType.StartsWith("erc20"))
            {
                Console.WriteLine($"║   Missing Logs:   {Interlocked.Read(ref _missingLogsCount),-45} ║");
            }
        }
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }

    private static async Task RunWorkerAsync(
        LoadTestConfig config,
        Account account,
        LoadTestMetrics metrics,
        CancellationToken ct)
    {
        var web3 = new NethWeb3(account, config.RpcUrl);
        web3.TransactionManager.UseLegacyAsDefault = true;

        // Use InMemoryNonceService to avoid nonce collisions with concurrent sends
        var nonceService = new InMemoryNonceService(account.Address, web3.Client);
        account.NonceService = nonceService;

        var random = new Random();
        var targetAddress = config.TargetAddress ?? "0x0000000000000000000000000000000000000001";

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                switch (config.TxType.ToLower())
                {
                    case "transfer":
                        var receipt = await web3.Eth.GetEtherTransferService()
                            .TransferEtherAndWaitForReceiptAsync(targetAddress, 0.001m);
                        sw.Stop();

                        if (receipt.Succeeded())
                        {
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "TX failed");
                        }
                        break;

                    case "call":
                        var callResult = await web3.Eth.Transactions.Call.SendRequestAsync(
                            new Nethereum.RPC.Eth.DTOs.CallInput
                            {
                                To = targetAddress,
                                Data = "0x"
                            });
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "noop":
                        // Just measure RPC round-trip
                        var blockNum = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "logs":
                        // Test eth_getLogs performance
                        var filterInput = new Nethereum.RPC.Eth.DTOs.NewFilterInput
                        {
                            FromBlock = new BlockParameter(0),
                            ToBlock = BlockParameter.CreateLatest()
                        };
                        var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "receipt":
                        // First send a tx, then query its receipt
                        var txReceipt = await web3.Eth.GetEtherTransferService()
                            .TransferEtherAndWaitForReceiptAsync(targetAddress, 0.0001m);
                        if (txReceipt?.TransactionHash != null)
                        {
                            // Query the receipt again to measure retrieval
                            var retrievedReceipt = await web3.Eth.Transactions.GetTransactionReceipt
                                .SendRequestAsync(txReceipt.TransactionHash);
                            sw.Stop();
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            sw.Stop();
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "No receipt");
                        }
                        break;

                    case "block":
                        // Test eth_getBlockByNumber performance
                        var latestBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        var blockToFetch = random.Next(0, (int)latestBlock.Value + 1);
                        var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                            .SendRequestAsync(new BlockParameter((ulong)blockToFetch));
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "deploy":
                        // Deploy a simple storage contract
                        var deployReceipt = await DeploySimpleStorageAsync(web3);
                        sw.Stop();
                        if (deployReceipt?.Succeeded() == true)
                        {
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "Deploy failed");
                        }
                        break;

                    case "estimategas":
                        // Test eth_estimateGas performance
                        var estimateInput = new Nethereum.RPC.Eth.DTOs.CallInput
                        {
                            From = account.Address,
                            To = targetAddress,
                            Value = new HexBigInteger(1000)
                        };
                        var gasEstimate = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(estimateInput);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "proof":
                        // Test eth_getProof performance (Merkle proof generation)
                        var proofAddress = _benchmarkContractAddress ?? targetAddress;
                        var proofSlot = random.Next(0, Math.Max(1, config.SlotCount));
                        try
                        {
                            var proof = await web3.Eth.GetProof.SendRequestAsync(
                                proofAddress,
                                new string[] { "0x" + proofSlot.ToString("x") },
                                BlockParameter.CreateLatest());
                            sw.Stop();
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        catch (Exception proofEx)
                        {
                            sw.Stop();
                            metrics.RecordFailure(sw.ElapsedMilliseconds, $"Proof failed: {proofEx.Message}");
                        }
                        break;

                    case "proof-verify":
                        // Test eth_getProof with full cryptographic verification
                        var pvAddress = _benchmarkContractAddress ?? targetAddress;
                        var pvSlot = random.Next(0, Math.Max(1, config.SlotCount));
                        try
                        {
                            var pvStorageKeys = new string[] { "0x" + pvSlot.ToString("x") };
                            var pvProof = await web3.Eth.GetProof.SendRequestAsync(
                                pvAddress, pvStorageKeys, BlockParameter.CreateLatest());

                            var pvBlock = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                                .SendRequestAsync(BlockParameter.CreateLatest());
                            var pvStateRoot = pvBlock.StateRoot.HexToByteArray();

                            var pvAccount = pvProof.ToAccount();
                            var pvAccountValid = AccountProofVerification.VerifyAccountProofs(
                                pvAddress, pvStateRoot,
                                pvProof.AccountProofs.Select(x => x.HexToByteArray()),
                                pvAccount);

                            if (!pvAccountValid)
                            {
                                sw.Stop();
                                metrics.RecordFailure(sw.ElapsedMilliseconds, "Account proof verification failed");
                                break;
                            }

                            bool pvStorageValid = true;
                            foreach (var sp in pvProof.StorageProof)
                            {
                                if (sp.Proof == null || sp.Proof.Count == 0)
                                    continue;
                                var valid = StorageProofVerification.ValidateValueFromStorageProof(
                                    sp.Key.HexValue.HexToByteArray(),
                                    sp.Value.HexValue.HexToByteArray(),
                                    sp.Proof.Select(x => x.HexToByteArray()),
                                    pvProof.StorageHash.HexToByteArray());
                                if (!valid)
                                {
                                    pvStorageValid = false;
                                    break;
                                }
                            }

                            sw.Stop();
                            if (pvStorageValid)
                                metrics.RecordSuccess(sw.ElapsedMilliseconds);
                            else
                                metrics.RecordFailure(sw.ElapsedMilliseconds, "Storage proof verification failed");
                        }
                        catch (Exception pvEx)
                        {
                            sw.Stop();
                            metrics.RecordFailure(sw.ElapsedMilliseconds, $"Proof-verify failed: {pvEx.Message}");
                        }
                        break;

                    case "trace":
                        // Test debug_traceCall performance (full EVM execution trace)
                        var traceInput = new Nethereum.RPC.Eth.DTOs.CallInput
                        {
                            From = account.Address,
                            To = targetAddress,
                            Value = new HexBigInteger(0),
                            Data = "0x"
                        };
                        try
                        {
                            // Use debug_traceCall with minimal tracer config
                            var traceResult = await web3.Client.SendRequestAsync<object>(
                                new Nethereum.JsonRpc.Client.RpcRequest(
                                    Guid.NewGuid().ToString(),
                                    "debug_traceCall",
                                    traceInput,
                                    BlockParameter.CreateLatest().BlockNumber?.Value.ToString() ?? "latest",
                                    new { disableStorage = true, disableMemory = true, disableStack = true }));
                            sw.Stop();
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        catch (Exception traceEx)
                        {
                            sw.Stop();
                            metrics.RecordFailure(sw.ElapsedMilliseconds, $"Trace failed: {traceEx.Message}");
                        }
                        break;

                    case "storage-setup":
                        // Setup: populate storage slots in benchmark contract
                        if (string.IsNullOrEmpty(_benchmarkContractAddress))
                        {
                            metrics.RecordFailure(0, "No benchmark contract deployed");
                            break;
                        }
                        var slotIndex = random.Next(0, config.SlotCount);
                        var storageSetData = EncodeFunctionCall("setStorage",
                            new object[] { new BigInteger(slotIndex), new BigInteger(random.Next()) });
                        var storageSetReceipt = await SendContractTxAsync(web3, _benchmarkContractAddress, storageSetData);
                        sw.Stop();
                        if (storageSetReceipt?.Succeeded() == true)
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        else
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "Storage set failed");
                        break;

                    case "storage-read":
                        // Read storage slots from Create2Factory or any address
                        // Tests eth_getStorageAt performance
                        var targetAddr = _benchmarkContractAddress ?? "0x4e59b44847b379578588920cA78FbF26c0B4956C";
                        var readSlot = random.Next(0, Math.Max(1, config.SlotCount));
                        var storageValue = await web3.Eth.GetStorageAt.SendRequestAsync(
                            targetAddr,
                            new HexBigInteger(readSlot));
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "storage-read-multi":
                        // Read multiple storage slots in sequence (10 slots per request)
                        var multiTargetAddr = _benchmarkContractAddress ?? "0x4e59b44847b379578588920cA78FbF26c0B4956C";
                        for (int s = 0; s < 10; s++)
                        {
                            var multiSlot = random.Next(0, Math.Max(1, config.SlotCount));
                            await web3.Eth.GetStorageAt.SendRequestAsync(
                                multiTargetAddr,
                                new HexBigInteger(multiSlot));
                        }
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "logs-setup":
                        // Emit events from benchmark contract
                        if (string.IsNullOrEmpty(_benchmarkContractAddress))
                        {
                            metrics.RecordFailure(0, "No benchmark contract deployed");
                            break;
                        }
                        var emitData = EncodeFunctionCall("emitEvents", new object[] { new BigInteger(config.EventCount) });
                        var emitReceipt = await SendContractTxAsync(web3, _benchmarkContractAddress, emitData);
                        sw.Stop();
                        if (emitReceipt?.Succeeded() == true)
                        {
                            Interlocked.Add(ref _eventCount, config.EventCount);
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        }
                        else
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "Emit events failed");
                        break;

                    case "logs-range":
                        // Query logs across a block range with actual data
                        var rangeFilter = new Nethereum.RPC.Eth.DTOs.NewFilterInput
                        {
                            FromBlock = new BlockParameter((ulong)_setupBlockStart),
                            ToBlock = new BlockParameter((ulong)_setupBlockEnd),
                            Address = new[] { _benchmarkContractAddress }
                        };
                        var rangeLogs = await web3.Eth.Filters.GetLogs.SendRequestAsync(rangeFilter);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "logs-topic":
                        // Query logs filtered by topic (Ping event)
                        // Ping(uint256 indexed id, uint256 timestamp) = 0x2cbdbe00cebef89186c967208065ecaafca1aa8a8971c4ccaa8ac017a9cad9ae
                        var topicFilter = new Nethereum.RPC.Eth.DTOs.NewFilterInput
                        {
                            FromBlock = new BlockParameter((ulong)_setupBlockStart),
                            ToBlock = BlockParameter.CreateLatest(),
                            Address = new[] { _benchmarkContractAddress },
                            Topics = new object[] { "0x2cbdbe00cebef89186c967208065ecaafca1aa8a8971c4ccaa8ac017a9cad9ae" }
                        };
                        var topicLogs = await web3.Eth.Filters.GetLogs.SendRequestAsync(topicFilter);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "erc20-transfer":
                        // Real ERC20 transfer between test accounts
                        if (string.IsNullOrEmpty(_erc20ContractAddress) || _testAccountAddresses == null)
                        {
                            metrics.RecordFailure(0, "ERC20 not deployed");
                            break;
                        }
                        var erc20ToIndex = random.Next(0, _testAccountAddresses.Count);
                        var erc20To = _testAccountAddresses[erc20ToIndex];
                        var transferAmount = new BigInteger(1); // 1 token unit
                        var erc20TransferData = EncodeErc20Transfer(erc20To, transferAmount);
                        var erc20TxReceipt = await SendContractTxAsync(web3, _erc20ContractAddress, erc20TransferData);
                        sw.Stop();
                        if (erc20TxReceipt?.Succeeded() == true)
                            metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        else
                            metrics.RecordFailure(sw.ElapsedMilliseconds, "ERC20 transfer failed");
                        break;

                    case "erc20-balance":
                        // ERC20 balanceOf call
                        if (string.IsNullOrEmpty(_erc20ContractAddress))
                        {
                            metrics.RecordFailure(0, "ERC20 not deployed");
                            break;
                        }
                        var balanceOfData = EncodeErc20BalanceOf(account.Address);
                        var balanceResult = await web3.Eth.Transactions.Call.SendRequestAsync(
                            new Nethereum.RPC.Eth.DTOs.CallInput
                            {
                                To = _erc20ContractAddress,
                                Data = balanceOfData
                            });
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        break;

                    case "transfer-async":
                        // Send ETH transfer without waiting for receipt
                        var asyncTransferInput = new TransactionInput
                        {
                            From = account.Address,
                            To = targetAddress,
                            Value = new HexBigInteger(1000000000000000), // 0.001 ETH
                            Gas = new HexBigInteger(21000)
                        };
                        var asyncTransferHash = await web3.Eth.TransactionManager.SendTransactionAsync(asyncTransferInput);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds);
                        _pendingReceipts.Enqueue((asyncTransferHash, sw.ElapsedMilliseconds, Stopwatch.StartNew()));
                        break;

                    case "erc20-async":
                        // ERC20 transfer - fire and forget, receipt collected separately
                        if (string.IsNullOrEmpty(_erc20ContractAddress) || _testAccountAddresses == null)
                        {
                            metrics.RecordFailure(0, "ERC20 not deployed");
                            break;
                        }
                        var asyncToIndex = random.Next(0, _testAccountAddresses.Count);
                        var asyncTo = _testAccountAddresses[asyncToIndex];
                        var asyncTransferData = EncodeErc20Transfer(asyncTo, new BigInteger(1));
                        var asyncTxInput = new TransactionInput
                        {
                            From = account.Address,
                            To = _erc20ContractAddress,
                            Data = asyncTransferData,
                            Gas = new HexBigInteger(100000)
                        };
                        var asyncTxHash = await web3.Eth.TransactionManager.SendTransactionAsync(asyncTxInput);
                        sw.Stop();
                        metrics.RecordSuccess(sw.ElapsedMilliseconds); // Record send time
                        _pendingReceipts.Enqueue((asyncTxHash, sw.ElapsedMilliseconds, Stopwatch.StartNew()));
                        break;

                    default:
                        metrics.RecordFailure(0, $"Unknown tx type: {config.TxType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordFailure(sw.ElapsedMilliseconds, ex.Message);

                // Reset nonce on nonce-related errors to resync with chain
                if (ex.Message.Contains("nonce", StringComparison.OrdinalIgnoreCase))
                {
                    await nonceService.ResetNonceAsync();
                }
            }
        }
    }

    private static void PrintMetrics(LoadTestMetrics metrics)
    {
        var stats = metrics.GetStats();
        Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ");
        Console.Write($"TPS: {stats.CurrentTps,6:F1} | ");
        Console.Write($"Total: {stats.TotalSuccess,6} | ");
        Console.Write($"Failed: {stats.TotalFailed,4} | ");
        Console.Write($"Avg: {stats.AvgLatencyMs,6:F1}ms | ");
        Console.Write($"P99: {stats.P99LatencyMs,6:F1}ms");
    }

    private static void PrintFinalMetrics(LoadTestMetrics metrics, TimeSpan elapsed)
    {
        var stats = metrics.GetStats();
        var totalTps = stats.TotalSuccess / elapsed.TotalSeconds;

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

        var topErrors = metrics.GetTopErrors().ToList();
        if (topErrors.Count > 0)
        {
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Top Errors:                                                   ║");
            foreach (var (error, count) in topErrors)
            {
                var truncated = error.Length > 50 ? error.Substring(0, 47) + "..." : error;
                Console.WriteLine($"║   {count,4}x: {truncated,-52} ║");
            }
        }
    }

    private static async Task RunReceiptCollectorAsync(string rpcUrl, LoadTestMetrics metrics, CancellationToken ct)
    {
        var web3 = new NethWeb3(rpcUrl);
        var batchSize = 50;
        var isErc20Test = !string.IsNullOrEmpty(_erc20ContractAddress);

        while (!ct.IsCancellationRequested || !_pendingReceipts.IsEmpty)
        {
            var toCheck = new List<(string TxHash, long SendTimeMs, Stopwatch Sw)>();

            // Dequeue a batch
            while (toCheck.Count < batchSize && _pendingReceipts.TryDequeue(out var item))
            {
                toCheck.Add(item);
            }

            if (toCheck.Count == 0)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                continue;
            }

            // Check receipts in parallel
            var tasks = toCheck.Select(async item =>
            {
                try
                {
                    var receipt = await web3.Eth.Transactions.GetTransactionReceipt
                        .SendRequestAsync(item.TxHash).ConfigureAwait(false);

                    if (receipt != null)
                    {
                        item.Sw.Stop();

                        // Validate receipt status (1 = success, 0 = reverted)
                        if (receipt.Status?.Value != 1)
                        {
                            Interlocked.Increment(ref _revertedCount);
                            return true; // Still confirmed, just reverted
                        }

                        // For ERC20 tests, validate Transfer event log
                        if (isErc20Test)
                        {
                            var hasTransferLog = receipt.Logs?.Any(log =>
                                log.Topics != null &&
                                log.Topics.Length > 0 &&
                                log.Topics[0]?.ToString()?.Equals(ERC20_TRANSFER_TOPIC, StringComparison.OrdinalIgnoreCase) == true) ?? false;

                            if (!hasTransferLog)
                            {
                                Interlocked.Increment(ref _missingLogsCount);
                            }
                        }

                        Interlocked.Increment(ref _confirmedCount);
                        return true;
                    }
                    else
                    {
                        // Not yet confirmed, re-queue if still running
                        if (!ct.IsCancellationRequested)
                            _pendingReceipts.Enqueue(item);
                        return false;
                    }
                }
                catch
                {
                    Interlocked.Increment(ref _failedConfirmCount);
                    return false;
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private static async Task<TransactionReceipt?> DeploySimpleStorageAsync(NethWeb3 web3)
    {
        // Minimal contract that stores a single uint256 value
        // Runtime code: PUSH1 0x00 SLOAD PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
        // Init code: PUSH the runtime code into memory and RETURN it
        // This is a minimal storage contract - stores at slot 0
        const string bytecode = "0x600a600c600039600a6000f3fe60005460005260206000f3";

        var transactionInput = new TransactionInput
        {
            From = web3.TransactionManager.Account?.Address,
            Data = bytecode,
            Gas = new HexBigInteger(500000)
        };

        var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(transactionInput);
        return await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
    }

    private static async Task<string?> DeployBenchmarkContractAsync(NethWeb3 web3)
    {
        // EventEmitter contract - compiled with solc 0.8.19
        // Source: EventEmitter.sol
        // Functions:
        //   setStorage(uint256,uint256) = 0x936ad72f - stores value, emits DataSet event
        //   emitEvents(uint256) = 0xd7d58f5b - emits N Ping events
        //   emitSingle() = 0x3701e169 - emits 1 Ping event
        //   data(uint256) = 0xf0ba8440 - view mapping
        // Events:
        //   DataSet(uint256 indexed key, uint256 value) = 0xc911a63e...
        //   Ping(uint256 indexed id, uint256 timestamp) = 0x2cbdbe00...
        const string bytecode = "0x608060405234801561001057600080fd5b5061022b806100206000396000f3fe608060405234801561001057600080fd5b506004361061004c5760003560e01c80633701e16914610051578063936ad72f1461005b578063d7d58f5b1461006e578063f0ba844014610081575b600080fd5b6100596100b3565b005b610059610069366004610193565b6100ef565b61005961007c3660046101b5565b610138565b6100a161008f3660046101b5565b60006020819052908152604090205481565b60405190815260200160405180910390f35b437f2cbdbe00cebef89186c967208065ecaafca1aa8a8971c4ccaa8ac017a9cad9ae426040516100e591815260200190565b60405180910390a2565b60008281526020818152604091829020839055905182815283917fc911a63e29945a04493ec58a89a96aa00a33c2609f1c96d4e506a7fb094e4c94910160405180910390a25050565b60005b8181101561018f57807f2cbdbe00cebef89186c967208065ecaafca1aa8a8971c4ccaa8ac017a9cad9ae4260405161017591815260200190565b60405180910390a280610187816101ce565b91505061013b565b5050565b600080604083850312156101a657600080fd5b50508035926020909101359150565b6000602082840312156101c757600080fd5b5035919050565b6000600182016101ee57634e487b7160e01b600052601160045260246000fd5b506001019056fea26469706673582212208d7d4dc4d4f4c88d41af522f11db998baee0537301b858e65d663beb54a2cad064736f6c63430008130033";

        var transactionInput = new TransactionInput
        {
            From = web3.TransactionManager.Account?.Address,
            Data = bytecode,
            Gas = new HexBigInteger(2000000)
        };

        var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(transactionInput);

        // Poll for receipt
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(500);
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            if (receipt != null)
            {
                return receipt.ContractAddress;
            }
        }
        return null;
    }

    private static string EncodeFunctionCall(string functionName, object[] parameters)
    {
        switch (functionName)
        {
            case "setStorage":
                // setStorage(uint256 key, uint256 value) = 0x936ad72f
                var key = ((BigInteger)parameters[0]).ToString("x64");
                var value = ((BigInteger)parameters[1]).ToString("x64");
                return "0x936ad72f" + key + value;

            case "emitEvents":
                // emitEvents(uint256 count) = 0xd7d58f5b
                var count = ((BigInteger)parameters[0]).ToString("x64");
                return "0xd7d58f5b" + count;

            case "emitSingle":
                // emitSingle() = 0x3701e169
                return "0x3701e169";

            default:
                throw new ArgumentException($"Unknown function: {functionName}");
        }
    }

    // ERC20 function encodings
    // transfer(address to, uint256 amount) = 0xa9059cbb
    private static string EncodeErc20Transfer(string to, BigInteger amount)
    {
        var toAddress = to.Replace("0x", "").PadLeft(64, '0');
        var amountHex = amount.ToString("x64");
        return "0xa9059cbb" + toAddress + amountHex;
    }

    // balanceOf(address account) = 0x70a08231
    private static string EncodeErc20BalanceOf(string account)
    {
        var accountAddress = account.Replace("0x", "").PadLeft(64, '0');
        return "0x70a08231" + accountAddress;
    }

    // mint(address to, uint256 amount) = 0x40c10f19
    private static string EncodeErc20Mint(string to, BigInteger amount)
    {
        var toAddress = to.Replace("0x", "").PadLeft(64, '0');
        var amountHex = amount.ToString("x64");
        return "0x40c10f19" + toAddress + amountHex;
    }

    private static async Task<string?> DeployERC20Async(NethWeb3 web3, BigInteger initialSupply)
    {
        // EIP20 Token bytecode from Nethereum tests - takes constructor args:
        // constructor(uint256 _initialAmount, string _tokenName, uint8 _decimalUnits, string _tokenSymbol)
        const string bytecode = "608060405234801561001057600080fd5b506040516107843803806107848339810160409081528151602080840151838501516060860151336000908152808552959095208490556002849055908501805193959094919391019161006991600391860190610096565b506004805460ff191660ff8416179055805161008c906005906020840190610096565b5050505050610131565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100d757805160ff1916838001178555610104565b82800160010185558215610104579182015b828111156101045782518255916020019190600101906100e9565b50610110929150610114565b5090565b61012e91905b80821115610110576000815560010161011a565b90565b610644806101406000396000f3006080604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017557806323b872dd1461019c57806327e235e3146101c6578063313ce567146101e75780635c6581651461021257806370a082311461023957806395d89b411461025a578063a9059cbb1461026f578063dd62ed3e14610293575b600080fd5b3480156100bf57600080fd5b506100c86102ba565b6040805160208082528351818301528351919283929083019185019080838360005b838110156101025781810151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561014957600080fd5b50610161600160a060020a0360043516602435610348565b604080519115158252519081900360200190f35b34801561018157600080fd5b5061018a6103ae565b60408051918252519081900360200190f35b3480156101a857600080fd5b50610161600160a060020a03600435811690602435166044356103b4565b3480156101d257600080fd5b5061018a600160a060020a03600435166104b7565b3480156101f357600080fd5b506101fc6104c9565b6040805160ff9092168252519081900360200190f35b34801561021e57600080fd5b5061018a600160a060020a03600435811690602435166104d2565b34801561024557600080fd5b5061018a600160a060020a03600435166104ef565b34801561026657600080fd5b506100c861050a565b34801561027b57600080fd5b50610161600160a060020a0360043516602435610565565b34801561029f57600080fd5b5061018a600160a060020a03600435811690602435166105ed565b6003805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b820191906000526020600020905b81548152906001019060200180831161032357829003601f168201915b505050505081565b336000818152600160209081526040808320600160a060020a038716808552908352818420869055815186815291519394909390927f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925928290030190a350600192915050565b60025481565b600160a060020a03831660008181526001602090815260408083203384528252808320549383529082905281205490919083118015906103f45750828110155b15156103ff57600080fd5b600160a060020a038085166000908152602081905260408082208054870190559187168152208054849003905560001981101561046157600160a060020a03851660009081526001602090815260408083203384529091529020805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef856040518082815260200191505060405180910390a3506001949350505050565b60006020819052908152604090205481565b60045460ff1681565b600160209081526000928352604080842090915290825290205481565b600160a060020a031660009081526020819052604090205490565b6005805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b3360009081526020819052604081205482111561058157600080fd5b3360008181526020818152604080832080548790039055600160a060020a03871680845292819020805487019055805186815290519293927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef929181900390910190a350600192915050565b600160a060020a039182166000908152600160209081526040808320939094168252919091522054905600a165627a7a72305820a364c08a705d8b29603263ebff0569de6c90b2d665d056a3c77729e2eda923ef0029";

        // Encode constructor args manually: (uint256 initialAmount, string name, uint8 decimals, string symbol)
        // Layout: initialAmount(32) + name_offset(32) + decimals(32) + symbol_offset(32) + name_data + symbol_data
        var initialAmountHex = initialSupply.ToString("x64");
        var decimalsHex = "12".PadLeft(64, '0'); // 18 decimals = 0x12
        // Offsets for dynamic strings (after 4 static params = 0x80)
        var nameOffset = "80".PadLeft(64, '0');   // 128 = 0x80
        var symbolOffset = "c0".PadLeft(64, '0'); // 192 = 0xc0
        // Name: "TestToken" (9 chars)
        var nameLen = "09".PadLeft(64, '0');
        var nameData = "54657374546f6b656e".PadRight(64, '0'); // "TestToken" in hex
        // Symbol: "TEST" (4 chars)
        var symbolLen = "04".PadLeft(64, '0');
        var symbolData = "54455354".PadRight(64, '0'); // "TEST" in hex

        var constructorArgs = initialAmountHex + nameOffset + decimalsHex + symbolOffset + nameLen + nameData + symbolLen + symbolData;

        var transactionInput = new TransactionInput
        {
            From = web3.TransactionManager.Account?.Address,
            Data = bytecode + constructorArgs,
            Gas = new HexBigInteger(3000000)
        };

        var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(transactionInput);

        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(200);
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            if (receipt != null && receipt.ContractAddress != null)
            {
                return receipt.ContractAddress;
            }
        }
        return null;
    }

    private static async Task<TransactionReceipt?> SendContractTxAsync(NethWeb3 web3, string contractAddress, string data)
    {
        var transactionInput = new TransactionInput
        {
            From = web3.TransactionManager.Account?.Address,
            To = contractAddress,
            Data = data,
            Gas = new HexBigInteger(500000)
        };

        var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(transactionInput);

        // Poll for receipt
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(200);
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            if (receipt != null)
            {
                return receipt;
            }
        }
        return null;
    }

    private static BigInteger CalculateMappingSlot(int mappingSlot, int key)
    {
        // For mapping at slot N with key K, storage slot = keccak256(K . N)
        // where . is concatenation and both are padded to 32 bytes
        var sha3 = new Sha3Keccack();
        var keyBytes = new byte[32];
        var slotBytes = new byte[32];

        // Key as big-endian 32 bytes
        var keyBigInt = new BigInteger(key);
        var keyArr = keyBigInt.ToByteArray();
        Array.Reverse(keyArr);
        Array.Copy(keyArr, 0, keyBytes, 32 - keyArr.Length, keyArr.Length);

        // Slot as big-endian 32 bytes
        var slotBigInt = new BigInteger(mappingSlot);
        var slotArr = slotBigInt.ToByteArray();
        Array.Reverse(slotArr);
        Array.Copy(slotArr, 0, slotBytes, 32 - slotArr.Length, slotArr.Length);

        // Concatenate and hash
        var concat = new byte[64];
        Array.Copy(keyBytes, 0, concat, 0, 32);
        Array.Copy(slotBytes, 0, concat, 32, 32);

        var hash = sha3.CalculateHash(concat);
        return new BigInteger(hash, isUnsigned: true, isBigEndian: true);
    }

    private static async Task SetupBenchmarkContractAsync(LoadTestConfig config, NethWeb3 masterWeb3)
    {
        if (!string.IsNullOrEmpty(config.ContractAddress))
        {
            _benchmarkContractAddress = config.ContractAddress;
            Console.WriteLine($"Using existing benchmark contract: {_benchmarkContractAddress}");
        }
        else if (config.TxType.StartsWith("storage-") || config.TxType.StartsWith("logs-"))
        {
            Console.WriteLine("Deploying benchmark contract...");
            _benchmarkContractAddress = await DeployBenchmarkContractAsync(masterWeb3);
            if (_benchmarkContractAddress == null)
            {
                Console.WriteLine("ERROR: Failed to deploy benchmark contract!");
                return;
            }
            Console.WriteLine($"Benchmark contract deployed at: {_benchmarkContractAddress}");
        }

        _setupBlockStart = await masterWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        _storageSlotCount = config.SlotCount;

        // Pre-populate some data for read tests (only if we have a working contract)
        if ((config.TxType == "storage-read" || config.TxType == "storage-read-multi")
            && !string.IsNullOrEmpty(_benchmarkContractAddress))
        {
            Console.WriteLine($"Attempting to pre-populate {config.SlotCount} storage slots...");
            var random = new Random(42);
            try
            {
                for (int i = 0; i < Math.Min(10, config.SlotCount); i++)
                {
                    var data = EncodeFunctionCall("setStorage",
                        new object[] { new BigInteger(i), new BigInteger(random.Next()) });
                    var receipt = await SendContractTxAsync(masterWeb3, _benchmarkContractAddress!, data);
                    if (receipt == null || !receipt.Succeeded())
                    {
                        Console.WriteLine($"\n  Storage population failed at slot {i}, using empty storage for reads.");
                        break;
                    }
                    Console.Write($"\r  Populated {i + 1}/{config.SlotCount} slots...");
                }
                Console.WriteLine($"\r  Storage setup complete.                              ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Storage population error: {ex.Message}");
                Console.WriteLine("  Continuing with empty storage reads...");
            }
            _storageSlotCount = config.SlotCount;
        }

        // Pre-emit some events for log query tests (skip if contract isn't working)
        if ((config.TxType == "logs-range" || config.TxType == "logs-topic")
            && !string.IsNullOrEmpty(_benchmarkContractAddress))
        {
            Console.WriteLine($"Attempting to pre-emit events...");
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var data = EncodeFunctionCall("emitEvents", new object[] { new BigInteger(config.EventCount) });
                    var receipt = await SendContractTxAsync(masterWeb3, _benchmarkContractAddress!, data);
                    if (receipt == null || !receipt.Succeeded())
                    {
                        Console.WriteLine($"\n  Event emission failed, logs-range will query empty results.");
                        break;
                    }
                    Console.Write($"\r  Emitted {(i + 1) * config.EventCount} events...");
                }
                Console.WriteLine($"\r  Event emission complete.                           ");
                _eventCount = 3 * config.EventCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Event emission error: {ex.Message}");
                Console.WriteLine("  logs-range will query existing logs in block range.");
            }
        }

        _setupBlockEnd = await masterWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Console.WriteLine($"Setup complete. Block range: {_setupBlockStart} - {_setupBlockEnd}");
    }

    private static async Task SetupERC20ContractAsync(LoadTestConfig config, NethWeb3 masterWeb3, List<Account> accounts)
    {
        Console.WriteLine("Deploying ERC20 token...");
        var totalSupply = BigInteger.Parse("1000000000000000000000000000"); // 1B tokens (for long tests)
        _erc20ContractAddress = await DeployERC20Async(masterWeb3, totalSupply);

        if (_erc20ContractAddress == null)
        {
            Console.WriteLine("ERROR: Failed to deploy ERC20 contract!");
            return;
        }
        Console.WriteLine($"ERC20 deployed at: {_erc20ContractAddress}");

        // Transfer tokens to test accounts (master has all initial supply)
        Console.WriteLine("Distributing tokens to test accounts...");
        var amountPerAccount = BigInteger.Parse("10000000000000000000000000"); // 10M tokens each (enough for millions of transfers)

        for (int i = 0; i < accounts.Count; i++)
        {
            var transferData = EncodeErc20Transfer(accounts[i].Address, amountPerAccount);
            var receipt = await SendContractTxAsync(masterWeb3, _erc20ContractAddress!, transferData);
            if (receipt == null || !receipt.Succeeded())
            {
                Console.WriteLine($"\n  Transfer failed for account {i}");
            }
            else
            {
                Console.Write($"\r  Funded {i + 1}/{accounts.Count} accounts...");
            }
        }
        Console.WriteLine($"\r  Token distribution complete.                    ");
    }

    private static async Task RunProofScaleBenchmarkAsync(LoadTestConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              Proof Scale Benchmark                            ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ RPC URL:        {config.RpcUrl,-47} ║");
        Console.WriteLine($"║ Proofs/Tier:    {config.Concurrency,-47} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var masterAccount = new Account(config.PrivateKey, 420420);
        var masterWeb3 = new NethWeb3(masterAccount, config.RpcUrl);
        masterWeb3.TransactionManager.UseLegacyAsDefault = true;

        var tiers = new[] { 100, 500, 1000, 5000, 10000, 50000 };
        var proofsPerTier = config.Concurrency;
        var allFundedAddresses = new List<string>();
        var tierResults = new List<(int accounts, int proofs, double avg, double p50, double p95, double p99, int verified, int failed)>();

        Console.WriteLine("Phase 1: Progressive state population...");
        Console.WriteLine();

        foreach (var tier in tiers)
        {
            var needed = tier - allFundedAddresses.Count;
            if (needed > 0)
            {
                Console.Write($"  Funding {needed} accounts to reach {tier} total...");
                var txHashes = new List<string>();
                for (int i = 0; i < needed; i++)
                {
                    var key = EthECKey.GenerateKey();
                    var addr = key.GetPublicAddress();
                    allFundedAddresses.Add(addr);

                    var txHash = await masterWeb3.Eth.GetEtherTransferService()
                        .TransferEtherAsync(addr, 0.001m);
                    txHashes.Add(txHash);
                }

                // Wait for last TX
                if (txHashes.Count > 0)
                {
                    var lastHash = txHashes.Last();
                    for (int w = 0; w < 60; w++)
                    {
                        var r = await masterWeb3.Eth.Transactions.GetTransactionReceipt
                            .SendRequestAsync(lastHash);
                        if (r != null) break;
                        await Task.Delay(200);
                    }
                }
                Console.WriteLine(" done.");
            }

            // Phase 2: Measure proof latency at this tier
            Console.Write($"  Measuring {proofsPerTier} proofs at {tier} accounts...");
            var latencies = new List<long>();
            int verifiedCount = 0;
            int failedCount = 0;
            var random = new Random();

            for (int p = 0; p < proofsPerTier; p++)
            {
                var targetAddr = allFundedAddresses[random.Next(allFundedAddresses.Count)];
                var sw = Stopwatch.StartNew();
                try
                {
                    var proof = await masterWeb3.Eth.GetProof.SendRequestAsync(
                        targetAddr, Array.Empty<string>(), BlockParameter.CreateLatest());
                    sw.Stop();
                    latencies.Add(sw.ElapsedMilliseconds);

                    var block = await masterWeb3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                        .SendRequestAsync(BlockParameter.CreateLatest());
                    var stateRoot = block.StateRoot.HexToByteArray();
                    var account = proof.ToAccount();
                    var valid = AccountProofVerification.VerifyAccountProofs(
                        targetAddr, stateRoot,
                        proof.AccountProofs.Select(x => x.HexToByteArray()), account);

                    if (valid) verifiedCount++;
                    else failedCount++;
                }
                catch
                {
                    sw.Stop();
                    latencies.Add(sw.ElapsedMilliseconds);
                    failedCount++;
                }
            }

            latencies.Sort();
            var avg = latencies.Count > 0 ? latencies.Average() : 0;
            var p50 = latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.50)] : 0;
            var p95 = latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.95)] : 0;
            var p99 = latencies.Count > 0 ? latencies[Math.Min((int)(latencies.Count * 0.99), latencies.Count - 1)] : 0;

            tierResults.Add((tier, proofsPerTier, avg, p50, p95, p99, verifiedCount, failedCount));
            Console.WriteLine($" avg={avg:F1}ms p99={p99}ms verified={verifiedCount}/{proofsPerTier}");
        }

        // Print summary table
        Console.WriteLine();
        Console.WriteLine("╔══════════╤════════╤═════════╤═════════╤═════════╤═════════╤══════════╗");
        Console.WriteLine("║ Accounts │ Proofs │ Avg(ms) │ P50(ms) │ P95(ms) │ P99(ms) │ Verified ║");
        Console.WriteLine("╠══════════╪════════╪═════════╪═════════╪═════════╪═════════╪══════════╣");
        foreach (var (accounts, proofs, avg, p50, p95, p99, verified, failed) in tierResults)
        {
            Console.WriteLine($"║ {accounts,8:N0} │ {proofs,6} │ {avg,7:F1} │ {p50,7:F1} │ {p95,7:F1} │ {p99,7:F1} │ {verified,4}/{proofs,-3} ║");
        }
        Console.WriteLine("╚══════════╧════════╧═════════╧═════════╧═════════╧═════════╧══════════╝");
    }
}

public class LoadTestConfig
{
    public string RpcUrl { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public int Concurrency { get; set; } = 10;
    public int DurationSeconds { get; set; } = 30;
    public string TxType { get; set; } = "transfer";
    public string? TargetAddress { get; set; }
    public int WarmupSeconds { get; set; } = 5;
    public int SlotCount { get; set; } = 100;
    public int EventCount { get; set; } = 50;
    public string? ContractAddress { get; set; }
}

public class LoadTestMetrics
{
    private long _totalSuccess;
    private long _totalFailed;
    private long _lastSecondCount;
    private long _lastSecondTime;
    private double _peakTps;
    private readonly ConcurrentBag<long> _latencies = new();
    private readonly ConcurrentBag<string> _errors = new();
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
        _errors.Add(error);
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

    public LoadTestStats GetStats()
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

        return new LoadTestStats
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

    public IEnumerable<(string Error, int Count)> GetTopErrors(int count = 5)
    {
        return _errors
            .GroupBy(e => e.Length > 100 ? e.Substring(0, 100) : e)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => (g.Key, g.Count()));
    }

    private static double GetPercentile(long[] sorted, int percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

public class LoadTestStats
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
