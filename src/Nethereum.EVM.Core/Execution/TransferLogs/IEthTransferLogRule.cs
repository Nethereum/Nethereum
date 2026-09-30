using System.Collections.Generic;
using Nethereum.Util;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif

namespace Nethereum.EVM.Execution.TransferLogs
{
    /// <summary>
    /// Whether a fork records ETH movement as a log. Pre-Amsterdam no fork
    /// does, so <see cref="Rules.NoEthTransferLogRule"/> is the rule every
    /// fork up to and including Osaka wires; EIP-7708 (Amsterdam) is
    /// <see cref="Rules.Eip7708EthTransferLogRule"/>.
    ///
    /// <para>NOT every balance move. EIP-7708 logs four triggers, each
    /// nonzero-value and to a different account: the transaction, <c>CALL</c>,
    /// <c>SELFDESTRUCT</c>, and <c>CREATE</c>/<c>CREATE2</c>. Its Rationale
    /// excludes priority fees and the base-fee burn ("already derivable from
    /// the block header") and withdrawals ("not attached to a transaction,
    /// which means there is no natural emission point"). Adding a withdrawal
    /// log here would change the receipts root on this node and nowhere
    /// else.</para>
    ///
    /// <para>The sink is the log list of the frame the transfer belongs to,
    /// which is what gives the emission its revert semantics for free: a
    /// frame that fails discards its own logs, so a transfer that was rolled
    /// back leaves no record behind.</para>
    /// </summary>
    public interface IEthTransferLogRule
    {
#if EVM_SYNC
        void Emit(List<EvmLog> logs, string from, string to, EvmUInt256 value);
#else
        void Emit(List<FilterLog> logs, string from, string to, EvmUInt256 value);
#endif
    }
}
