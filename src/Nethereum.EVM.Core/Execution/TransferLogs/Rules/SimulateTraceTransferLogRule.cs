using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif

namespace Nethereum.EVM.Execution.TransferLogs.Rules
{
    /// <summary>
    /// eth_simulateV1 <c>traceTransfers</c>: geth synthesises a Transfer(from,to,value) log for each value
    /// movement at the fixed pseudo-address <c>0xeee…eee</c> (its <c>transferLogAddress</c>), which is distinct
    /// from EIP-7708's protocol transfer-log address. Only the emitting address differs from
    /// <see cref="Eip7708EthTransferLogRule"/>; the topic, indexed from/to and 32-byte value data are identical.
    /// </summary>
    public sealed class SimulateTraceTransferLogRule : IEthTransferLogRule
    {
        public static readonly SimulateTraceTransferLogRule Instance = new SimulateTraceTransferLogRule();

        public const string TransferTopic = Nethereum.Model.Erc20TransferEventTopic.Unprefixed;

        public const string TraceTransferAddress = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

#if EVM_SYNC
        public void Emit(List<EvmLog> logs, string from, string to, EvmUInt256 value)
#else
        public void Emit(List<FilterLog> logs, string from, string to, EvmUInt256 value)
#endif
        {
            if (logs == null)
                throw new ArgumentNullException(nameof(logs),
                    "eth_simulateV1 traceTransfers: no log sink to record an ETH transfer into.");

            if (value.IsZero) return;
            if (from.IsTheSameAddress(to)) return;

#if EVM_SYNC
            logs.Add(new EvmLog
#else
            logs.Add(new FilterLog
#endif
            {
                Address = TraceTransferAddress,
                Topics = new string[] { TransferTopic, AddressTopic(from), AddressTopic(to) },
                Data = value.ToBigEndian().ToHex()
            });
        }

        private static string AddressTopic(string address)
        {
            return address.HexToByteArray().PadTo32Bytes().ToHex();
        }
    }
}
