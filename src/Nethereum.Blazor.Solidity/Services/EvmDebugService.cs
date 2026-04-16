using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ABI.ABIRepository;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Debugging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;

namespace Nethereum.Blazor.Solidity.Services;

public class EvmDebugService : IEvmDebugService
{
    private readonly IWeb3 _web3;
    private readonly IABIInfoStorage _abiStorage;
    private readonly FileSystemABIInfoStorage? _fileSystemStorage;
    private readonly ILogger _logger;

    public bool IsAvailable => _web3 != null;

    public EvmDebugService(IWeb3 web3, IABIInfoStorage abiStorage, FileSystemABIInfoStorage? fileSystemStorage = null, ILogger<EvmDebugService>? logger = null)
    {
        _web3 = web3;
        _abiStorage = abiStorage;
        _fileSystemStorage = fileSystemStorage;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    public async Task<EvmReplayResult> ReplayTransactionAsync(string txHash)
    {
        var result = new EvmReplayResult();

        try
        {
            var txn = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);
            if (txn == null)
            {
                result.Error = "Transaction not found";
                return result;
            }

            if (string.IsNullOrEmpty(txn.To))
            {
                result.Error = "Contract creation transactions are not yet supported for debugging";
                return result;
            }

            var chainId = await _web3.Eth.ChainId.SendRequestAsync();
            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            List<ProgramTrace> trace = null;

            trace = await SimulateTransactionAsync(txn, chainId);

            if (trace == null || trace.Count == 0)
            {
                try
                {
                    trace = await GetTraceFromNodeAsync(txHash, txn.To);
                }
                catch
                {
                }
            }

            if (trace == null || trace.Count == 0)
            {
                var code = await _web3.Eth.GetCode.SendRequestAsync(txn.To, BlockParameter.CreateLatest());
                result.Error = (string.IsNullOrEmpty(code) || code == "0x")
                    ? "No contract code at target address. This may be a simple value transfer."
                    : "Transaction produced no execution trace";
                return result;
            }

            result.IsRevert = receipt?.Status?.Value == 0;
            result.TotalSteps = trace.Count;

            var matchedAbis = await MatchAbisForTraceAsync(trace, txn, chainId);

            var session = await trace.CreateDebugSessionAsync(_abiStorage, (long)chainId.Value);

            foreach (var kvp in matchedAbis)
            {
                session.SetContractDebugInfo(kvp.Key, kvp.Value);
            }

            result.Session = session;
            result.HasSourceMaps = session.HasDebugInfo();
            result.FileContents = session.GetAllSourceFileContents();

            var primaryAbi = session.GetABIInfoForAddress(txn.To);
            var primaryName = primaryAbi?.ContractName;

            result.SourceFiles = result.FileContents.Keys
                .OrderByDescending(f =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(f);
                    return string.Equals(fileName, primaryName, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            result.Error = $"Replay failed: {ex.Message}";
        }

        return result;
    }

    private async Task<List<ProgramTrace>> GetTraceFromNodeAsync(string txHash, string toAddress)
    {
        var options = new TracingOptions();
        var traceResult = await _web3.Debug.TraceTransaction.SendRequestAsync(txHash, options);
        if (traceResult == null)
            return null;

        var structLogs = traceResult["structLogs"] as JArray;
        if (structLogs == null || structLogs.Count == 0)
            return null;

        var failed = traceResult["failed"]?.Value<bool>() ?? false;
        var trace = new List<ProgramTrace>(structLogs.Count);

        for (int i = 0; i < structLogs.Count; i++)
        {
            var step = structLogs[i];
            var opName = step["op"]?.Value<string>() ?? "UNKNOWN";
            Enum.TryParse<Instruction>(opName, out var opcode);

            var pt = new ProgramTrace
            {
                VMTraceStep = i,
                ProgramTraceStep = i,
                Depth = (step["depth"]?.Value<int>() ?? 1) - 1,
                CodeAddress = step["address"]?.Value<string>() ?? toAddress,
                Instruction = new ProgramInstruction
                {
                    Step = step["pc"]?.Value<int>() ?? 0,
                    Instruction = opcode,
                },
                GasCost = step["gasCost"]?.Value<long>() ?? 0,
                GasRemaining = step["gas"]?.Value<long>() ?? 0,
            };

            var stackArray = step["stack"] as JArray;
            if (stackArray != null)
            {
                pt.Stack = stackArray.Select(s =>
                {
                    var hex = s.Value<string>() ?? "0x0";
                    if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        hex = hex.Substring(2);
                    return hex;
                }).ToList();
            }

            var storageObj = step["storage"] as JObject;
            if (storageObj != null)
            {
                pt.Storage = new Dictionary<string, string>();
                foreach (var prop in storageObj.Properties())
                {
                    pt.Storage[prop.Name] = prop.Value.Value<string>();
                }
            }

            var memoryToken = step["memory"];
            if (memoryToken != null && memoryToken.Type == JTokenType.String)
            {
                pt.Memory = memoryToken.Value<string>();
            }

            trace.Add(pt);
        }

        return trace;
    }

    private async Task<List<ProgramTrace>> SimulateTransactionAsync(
        Nethereum.RPC.Eth.DTOs.Transaction txn,
        HexBigInteger chainId)
    {
        var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
            .SendRequestAsync(txn.BlockNumber);

        var code = await _web3.Eth.GetCode.SendRequestAsync(txn.To, BlockParameter.CreateLatest());

        var preStateBlock = new BlockParameter(new HexBigInteger(txn.BlockNumber.Value - 1));
        if (string.IsNullOrEmpty(code) || code == "0x")
        {
            code = await _web3.Eth.GetCode.SendRequestAsync(txn.To, preStateBlock);
        }

        var codeBytes = (!string.IsNullOrEmpty(code) && code != "0x")
            ? code.HexToByteArray()
            : Array.Empty<byte>();

        if (codeBytes.Length == 0)
            return null;

        var callInput = new CallInput
        {
            From = txn.From,
            To = txn.To,
            Value = txn.Value,
            Data = txn.Input,
            Gas = txn.Gas,
            GasPrice = new HexBigInteger(0),
            ChainId = chainId
        };

        var nodeDataService = new RpcNodeDataService(_web3.Eth, preStateBlock);
        var executionStateService = new ExecutionStateService(nodeDataService);

        var callerBalance = await _web3.Eth.GetBalance.SendRequestAsync(txn.From, preStateBlock);
        executionStateService.SetInitialChainBalance(txn.From, callerBalance.Value);

        var programContext = new ProgramContext(
            callInput,
            executionStateService,
            txn.From,
            txn.To,
            (long)txn.BlockNumber.Value,
            (long)block.Timestamp.Value,
            block.Miner,
            (long)(block.BaseFeePerGas?.Value ?? 0));

        programContext.GasLimit = (long)block.GasLimit.Value;

        var program = new Program(codeBytes, programContext);
        var evmSimulator = new EVMSimulator(Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Osaka);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var simulationTask = evmSimulator.ExecuteWithCallStackAsync(program, 0, 0, true);
            var completedTask = await Task.WhenAny(simulationTask, Task.Delay(Timeout.Infinite, cts.Token));
            if (completedTask != simulationTask)
                return null;
            program = await simulationTask;
        }
        catch
        {
        }

        return program.Trace;
    }

    private async Task<Dictionary<string, ABIInfo>> MatchAbisForTraceAsync(
        List<ProgramTrace> trace,
        Nethereum.RPC.Eth.DTOs.Transaction txn,
        HexBigInteger chainId)
    {
        var matchedAbis = new Dictionary<string, ABIInfo>(StringComparer.OrdinalIgnoreCase);
        if (_fileSystemStorage == null)
            return matchedAbis;

        var traceAddresses = trace
            .Select(t => t.CodeAddress)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .ToList();

        _logger.LogDebug("Trace has {Count} distinct code addresses: {Addresses}",
            traceAddresses.Count, string.Join(", ", traceAddresses));

        foreach (var addr in traceAddresses)
        {
            var existing = await _abiStorage.GetABIInfoAsync((long)chainId.Value, addr);
            if (existing != null && existing.HasDebugInfo)
            {
                _logger.LogDebug("Address {Address} already has debug info ({ContractName}), skipping",
                    addr, existing.ContractName);
                continue;
            }

            var addrCode = await _web3.Eth.GetCode.SendRequestAsync(addr);
            _logger.LogDebug("Bytecode for {Address}: {Length} chars", addr, addrCode?.Length ?? 0);

            if (!string.IsNullOrEmpty(addrCode) && addrCode != "0x")
            {
                var matched = _fileSystemStorage.FindABIInfoByRuntimeBytecode(addrCode);
                if (matched != null && matched.HasDebugInfo)
                {
                    _logger.LogInformation("Matched {Address} → {ContractName} (sourceMap: {HasMap})",
                        addr, matched.ContractName, !string.IsNullOrEmpty(matched.RuntimeSourceMap));
                    matchedAbis[addr] = matched;
                    _fileSystemStorage.RegisterContractAddress(
                        matched.ContractName, addr, (long)chainId.Value);
                }
                else
                {
                    _logger.LogWarning("No bytecode match for {Address} (matched={IsMatched}, hasDebug={HasDebug})",
                        addr, matched != null, matched?.HasDebugInfo ?? false);
                }
            }
        }

        return matchedAbis;
    }
}
