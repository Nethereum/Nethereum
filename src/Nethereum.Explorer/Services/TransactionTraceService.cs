using Microsoft.Extensions.Logging;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.DebugNode.Tracers;

namespace Nethereum.Explorer.Services;

public class TransactionTraceService : ITransactionTraceService
{
    private readonly ExplorerWeb3Factory _web3Factory;
    private readonly ILogger<TransactionTraceService> _logger;
    private const int MaxCallDepth = 64;

    public TransactionTraceService(ExplorerWeb3Factory web3Factory, ILogger<TransactionTraceService> logger)
    {
        _web3Factory = web3Factory;
        _logger = logger;
    }

    public async Task<TransactionTraceResult?> TraceTransactionAsync(string txHash)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;

        var debugTrace = new DebugTraceTransaction(web3.Client);

        var options = new TracingOptions
        {
            TracerInfo = new CallTracerInfo(onlyTopCall: false, withLog: false)
        };

        try
        {
            var callTrace = await debugTrace.SendRequestAsync<CallTracerResponse>(txHash, options);
            if (callTrace == null)
                return new TransactionTraceResult { Error = "No trace data returned" };

            var result = new TransactionTraceResult { CallTrace = callTrace };
            FlattenCallTree(callTrace.Calls, 1, result.InternalCalls);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trace transaction {TxHash}", txHash);
            return new TransactionTraceResult { Error = "Failed to trace transaction. The node may not support debug_traceTransaction." };
        }
    }

    public async Task<StateDiffResult?> GetStateDiffAsync(string txHash)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;

        var debugTrace = new DebugTraceTransaction(web3.Client);

        var options = new TracingOptions
        {
            TracerInfo = new PrestateTracerInfo(diffMode: true)
        };

        try
        {
            var diffResponse = await debugTrace.SendRequestAsync<PrestateTracerResponseDiffMode>(txHash, options);
            if (diffResponse == null)
                return new StateDiffResult { Error = "No state diff data returned" };

            var result = new StateDiffResult();
            var allAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (diffResponse.Pre != null)
                foreach (var key in diffResponse.Pre.Keys)
                    allAddresses.Add(key);
            if (diffResponse.Post != null)
                foreach (var key in diffResponse.Post.Keys)
                    allAddresses.Add(key);

            foreach (var addr in allAddresses)
            {
                PrestateTracerResponseItem? pre = null;
                PrestateTracerResponseItem? post = null;
                diffResponse.Pre?.TryGetValue(addr, out pre);
                diffResponse.Post?.TryGetValue(addr, out post);

                var entry = new StateDiffEntry
                {
                    Address = addr,
                    BalanceBefore = pre?.Balance?.Value.ToString(),
                    BalanceAfter = post?.Balance?.Value.ToString(),
                    NonceBefore = pre?.Nonce,
                    NonceAfter = post?.Nonce,
                    CodeChanged = (pre?.Code ?? "") != (post?.Code ?? "")
                };

                var allSlots = new HashSet<string>();
                if (pre?.Storage != null) foreach (var k in pre.Storage.Keys) allSlots.Add(k);
                if (post?.Storage != null) foreach (var k in post.Storage.Keys) allSlots.Add(k);

                foreach (var slot in allSlots)
                {
                    var beforeVal = pre?.Storage?.GetValueOrDefault(slot);
                    var afterVal = post?.Storage?.GetValueOrDefault(slot);
                    if (beforeVal != afterVal)
                    {
                        entry.StorageChanges.Add(new StorageDiff
                        {
                            Slot = slot,
                            Before = beforeVal,
                            After = afterVal
                        });
                    }
                }

                result.Entries.Add(entry);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state diff for {TxHash}", txHash);
            return new StateDiffResult { Error = "Failed to load state diff. The node may not support prestateTracer." };
        }
    }

    private static void FlattenCallTree(List<CallTracerResponse>? calls, int depth, List<FlattenedInternalCall> output)
    {
        if (calls == null || depth > MaxCallDepth) return;
        foreach (var call in calls)
        {
            output.Add(new FlattenedInternalCall
            {
                Index = output.Count,
                Depth = depth,
                Type = call.Type ?? "CALL",
                From = call.From ?? "",
                To = call.To ?? "",
                Value = call.Value?.Value.ToString() ?? "0",
                Gas = call.Gas?.Value.ToString() ?? "0",
                GasUsed = call.GasUsed?.Value.ToString() ?? "0",
                Input = call.Input,
                Output = call.Output,
                Error = call.Error,
                RevertReason = call.RevertReason
            });
            FlattenCallTree(call.Calls, depth + 1, output);
        }
    }
}
