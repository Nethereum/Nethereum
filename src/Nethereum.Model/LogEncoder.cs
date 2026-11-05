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

        private const int AddressLength = 20;
        private const int TopicLength = 32;

        public Log Decode(byte[] rawdata)
        {
            var reader = new RLP.RlpReader(rawdata);
            var log = Decode(reader);
            reader.EnsureEnd();
            return log;
        }

        public Log Decode(RLP.RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var log = new Log();
            log.Address = reader.ReadFixed(AddressLength).ToHex(true);

            var topics = reader.ReadListHeader();
            log.Topics = new List<byte[]>();
            while (!reader.IsAtEnd(topics))
                log.Topics.Add(reader.ReadFixed(TopicLength));
            reader.EnsureEnd(topics);

            log.Data = reader.ReadBytes();
            reader.EnsureEnd(scope);
            return log;
        }

        public List<Log> DecodeList(RLP.RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var logs = new List<Log>();
            while (!reader.IsAtEnd(scope))
                logs.Add(Decode(reader));
            reader.EnsureEnd(scope);
            return logs;
        }
    }
}
