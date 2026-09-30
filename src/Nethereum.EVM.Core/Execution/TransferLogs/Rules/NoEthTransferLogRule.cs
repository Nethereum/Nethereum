using System.Collections.Generic;
using Nethereum.Util;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif

namespace Nethereum.EVM.Execution.TransferLogs.Rules
{
    public sealed class NoEthTransferLogRule : IEthTransferLogRule
    {
        public static readonly NoEthTransferLogRule Instance = new NoEthTransferLogRule();

#if EVM_SYNC
        public void Emit(List<EvmLog> logs, string from, string to, EvmUInt256 value)
        {
        }
#else
        public void Emit(List<FilterLog> logs, string from, string to, EvmUInt256 value)
        {
        }
#endif
    }
}
