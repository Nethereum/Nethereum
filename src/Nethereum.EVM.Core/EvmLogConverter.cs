using System.Collections.Generic;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
#if !EVM_SYNC
using Nethereum.RPC.Eth.DTOs;
#endif

namespace Nethereum.EVM
{
    public static class EvmLogConverter
    {
        public static Log ToModelLog(EvmLog evmLog)
        {
            var log = new Log
            {
                Address = evmLog.Address,
                Data = string.IsNullOrEmpty(evmLog.Data) || evmLog.Data == "0x"
                    ? new byte[0]
                    : evmLog.Data.HexToByteArray(),
                Topics = new List<byte[]>()
            };

            if (evmLog.Topics != null)
            {
                foreach (var topic in evmLog.Topics)
                {
                    log.Topics.Add(string.IsNullOrEmpty(topic)
                        ? new byte[32]
                        : topic.HexToByteArray());
                }
            }

            return log;
        }

        public static List<Log> ToModelLogs(List<EvmLog> evmLogs)
        {
            var result = new List<Log>();
            if (evmLogs == null) return result;
            foreach (var evmLog in evmLogs)
                result.Add(ToModelLog(evmLog));
            return result;
        }

#if !EVM_SYNC
        public static Log ToModelLog(FilterLog filterLog)
        {
            var log = new Log
            {
                Address = filterLog.Address,
                Data = string.IsNullOrEmpty(filterLog.Data) || filterLog.Data == "0x"
                    ? new byte[0]
                    : filterLog.Data.HexToByteArray(),
                Topics = new List<byte[]>()
            };

            if (filterLog.Topics != null)
            {
                foreach (var topic in filterLog.Topics)
                {
                    var topicStr = topic as string;
                    log.Topics.Add(string.IsNullOrEmpty(topicStr)
                        ? new byte[32]
                        : topicStr.HexToByteArray());
                }
            }

            return log;
        }

        public static List<Log> ToModelLogs(List<FilterLog> filterLogs)
        {
            var result = new List<Log>();
            if (filterLogs == null) return result;
            foreach (var filterLog in filterLogs)
                result.Add(ToModelLog(filterLog));
            return result;
        }
#endif
    }
}
