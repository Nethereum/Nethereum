using Nethereum.Documentation;
#if !EVM_SYNC
using System;
using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TransferLogs.Rules
{
    [NethereumDocExample(DocSection.EvmSimulator, "log-index", "Number ETH transfer logs from a shared counter")]
    public sealed class IndexingEthTransferLogRule : IEthTransferLogRule
    {
        private readonly IEthTransferLogRule _inner;
        private readonly LogIndexCounter _counter;

        public IndexingEthTransferLogRule(IEthTransferLogRule inner, LogIndexCounter counter)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        public void Emit(List<FilterLog> logs, string from, string to, EvmUInt256 value)
        {
            var before = logs.Count;
            _inner.Emit(logs, from, to, value);
            for (var i = before; i < logs.Count; i++)
                logs[i].LogIndex = new HexBigInteger(_counter.Next());
        }
    }
}
#endif
