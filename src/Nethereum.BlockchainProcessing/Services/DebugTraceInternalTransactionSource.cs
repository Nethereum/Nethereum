using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.DebugNode.Tracers;

namespace Nethereum.BlockchainProcessing.Services
{
    /// <summary>
    /// Internal-transaction source that calls geth-compatible
    /// <c>debug_traceTransaction</c> with the call tracer and flattens the nested
    /// response into <see cref="InternalTransaction"/> rows. Requires the node to
    /// expose the <c>debug</c> namespace; most public mainnet providers do not.
    /// </summary>
    public class DebugTraceInternalTransactionSource : IInternalTransactionSource
    {
        private readonly DebugTraceTransaction _debugTrace;
        private readonly TracingOptions _tracingOptions;

        public DebugTraceInternalTransactionSource(IClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _debugTrace = new DebugTraceTransaction(client);
            _tracingOptions = new TracingOptions
            {
                TracerInfo = new CallTracerInfo(onlyTopCall: false, withLog: false)
            };
        }

        public async Task<List<InternalTransaction>> ProduceAsync(string transactionHash)
        {
            var callTrace = await _debugTrace.SendRequestAsync<CallTracerResponse>(transactionHash, _tracingOptions)
                .ConfigureAwait(false);

            if (callTrace == null)
                return new List<InternalTransaction>();

            return InternalTransactionMapping.FlattenCallTrace(
                transactionHash,
                callTrace.Type,
                callTrace.From,
                callTrace.To,
                callTrace.Value?.Value.ToString() ?? "0",
                callTrace.Gas?.Value.ToString() ?? "0",
                callTrace.GasUsed?.Value.ToString() ?? "0",
                callTrace.Input,
                callTrace.Output,
                callTrace.Error,
                ConvertCalls(callTrace.Calls),
                revertReason: callTrace.RevertReason);
        }

        private static List<CallTraceEntry> ConvertCalls(List<CallTracerResponse> calls)
        {
            if (calls == null) return null;
            return calls.Select(c => new CallTraceEntry
            {
                Type = c.Type,
                From = c.From,
                To = c.To,
                Value = c.Value?.Value.ToString() ?? "0",
                Gas = c.Gas?.Value.ToString() ?? "0",
                GasUsed = c.GasUsed?.Value.ToString() ?? "0",
                Input = c.Input,
                Output = c.Output,
                Error = c.Error,
                RevertReason = c.RevertReason,
                Calls = ConvertCalls(c.Calls)
            }).ToList();
        }
    }
}
