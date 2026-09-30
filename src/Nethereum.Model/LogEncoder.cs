using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Model
{
    public class LogEncoder
    {
        public static LogEncoder Current { get; } = new LogEncoder();

        public byte[] Encode(Log log)
        {
            var encodedTopics = new byte[log.Topics.Count][];
            for (int i = 0; i < log.Topics.Count; i++)
                encodedTopics[i] = RLP.RLP.EncodeElement(log.Topics[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(log.Address.HexToByteArray()),
                RLP.RLP.EncodeList(encodedTopics),
                RLP.RLP.EncodeElement(log.Data ?? RLP.RLP.EMPTY_BYTE_ARRAY)
            );
        }

        public Log Decode(byte[] rawdata)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedElements = (RLP.RLPCollection)decodedList;

            var log = new Log();
            log.Address = decodedElements[0].RLPData.ToHex(true);

            var topicsCollection = (RLP.RLPCollection)decodedElements[1];
            log.Topics = new List<byte[]>();
            foreach (var topic in topicsCollection)
            {
                log.Topics.Add(topic.RLPData);
            }

            log.Data = decodedElements[2].RLPData;
            return log;
        }
    }
}
