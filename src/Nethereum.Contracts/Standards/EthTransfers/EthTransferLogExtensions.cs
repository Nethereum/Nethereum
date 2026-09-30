using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.Contracts.Standards.EthTransfers
{
    /// <summary>
    /// EIP-7708 (Amsterdam): moving ETH emits a
    /// <c>Transfer(address,address,uint256)</c> log, so value movement is visible to the
    /// same filters that already see ERC-20 movement. It is an ordinary Transfer event -
    /// it just comes from a different "contract", the protocol's system address.
    ///
    /// <para>Which is why <see cref="TransferEventDTO"/> is reused rather than a parallel
    /// DTO written: the signature, the two indexed address topics and the
    /// <c>uint256</c> value are the same, so an ETH transfer decodes through the type
    /// that already exists. The EMITTING ADDRESS is the only thing separating the two,
    /// and these helpers are where that separation is stated instead of being repeated at
    /// every call site.</para>
    ///
    /// <para>Anything asking "is this an ERC-20 transfer?" from the topic alone now
    /// answers yes for every ETH transfer on an Amsterdam chain. That is not the log
    /// being wrong; it is the question being under-specified.</para>
    /// </summary>
    public static class EthTransferLogExtensions
    {
        /// <summary>
        /// True when this log is an EIP-7708 ETH transfer: shaped like a Transfer event
        /// AND emitted by the system address.
        /// </summary>
        public static bool IsEthTransfer(this FilterLog log)
        {
            return log.IsLogForEventEmittedBy<TransferEventDTO>(AddressUtil.SYSTEM_ADDRESS);
        }

        public static bool IsErc20TransferAndNotEthTransfer(this FilterLog log)
        {
            if (log == null) return false;
            if (log.IsFromEthTransferEmitter()) return false;
            return log.IsLogForEvent<TransferEventDTO>();
        }

        public static bool IsFromEthTransferEmitter(this FilterLog log)
        {
            return log?.Address != null && log.Address.IsTheSameAddress(AddressUtil.SYSTEM_ADDRESS);
        }

        /// <summary>
        /// Decodes an EIP-7708 ETH transfer, returning null when the log is not one -
        /// including when it is a genuine ERC-20 transfer, which decodes identically and
        /// would otherwise be returned as if it were ETH.
        /// </summary>
        public static bool IsNativeTransfer(this EventLog<TransferEventDTO> transfer)
        {
            return transfer != null && transfer.Log.IsFromEthTransferEmitter();
        }

        public static IEnumerable<EventLog<TransferEventDTO>> NativeTransfers(
            this IEnumerable<EventLog<TransferEventDTO>> transfers)
        {
            if (transfers == null) yield break;
            foreach (var transfer in transfers)
                if (transfer.IsNativeTransfer()) yield return transfer;
        }

        public static IEnumerable<EventLog<TransferEventDTO>> TokenTransfers(
            this IEnumerable<EventLog<TransferEventDTO>> transfers)
        {
            if (transfers == null) yield break;
            foreach (var transfer in transfers)
                if (!transfer.IsNativeTransfer()) yield return transfer;
        }

        public static EventLog<TransferEventDTO> DecodeEthTransfer(string emitter, string[] topics, string data)
        {


            return new FilterLog
            {
                Address = emitter,
                Topics = topics,
                Data = data
            }.DecodeEthTransfer();
        }

        public static EventLog<TransferEventDTO> DecodeEthTransfer(this FilterLog log)
        {
            return log.IsEthTransfer() ? log.DecodeEventEmittedBy<TransferEventDTO>(AddressUtil.SYSTEM_ADDRESS) : null;
        }
    }
}
