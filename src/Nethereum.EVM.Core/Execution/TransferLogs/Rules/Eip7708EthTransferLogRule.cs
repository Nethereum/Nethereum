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
    public sealed class Eip7708EthTransferLogRule : IEthTransferLogRule
    {
        public static readonly Eip7708EthTransferLogRule Instance = new Eip7708EthTransferLogRule();

        public const string TransferTopic = Nethereum.Model.Erc20TransferEventTopic.Unprefixed;

        public const string SystemAddress = Nethereum.Util.AddressUtil.SYSTEM_ADDRESS;

#if EVM_SYNC
        public void Emit(List<EvmLog> logs, string from, string to, EvmUInt256 value)
#else
        public void Emit(List<FilterLog> logs, string from, string to, EvmUInt256 value)
#endif
        {
            if (logs == null)
                throw new ArgumentNullException(nameof(logs),
                    "EIP-7708: no log sink to record an ETH transfer into — dropping it silently would " +
                    "change the receipts root on this node and nowhere else.");

            if (value.IsZero) return;
            if (from.IsTheSameAddress(to)) return;

#if EVM_SYNC
            logs.Add(new EvmLog
#else
            logs.Add(new FilterLog
#endif
            {
                Address = SystemAddress,
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
