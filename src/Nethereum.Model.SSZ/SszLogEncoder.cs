using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;

namespace Nethereum.Model.SSZ
{
    // Log = Container (standard SSZ, not progressive)
    //   address: ExecutionAddress (ByteVector[20])
    //   topics: List[Bytes32, MAX_TOPICS_PER_LOG]
    //   data: ProgressiveByteList
    public class SszLogEncoder
    {
        public const int MaxTopicsPerLog = 4;
        public const int AddressLength = 20;
        public const int TopicLength = 32;

        public static readonly SszLogEncoder Current = new SszLogEncoder();

        public byte[] Encode(Log log)
        {
            var addressBytes = GetAddressBytes(log.Address);
            var topics = log.Topics ?? new List<byte[]>();
            var data = log.Data ?? Array.Empty<byte>();

            // SSZ Container with dynamic fields (topics list + data)
            // Fixed section: address(20) + topics_offset(4) + data_offset(4)
            var fixedSize = AddressLength + 4 + 4;
            var topicsSize = topics.Count * TopicLength;

            using var writer = new SszWriter();
            // Fixed: address
            writer.WriteFixedBytes(addressBytes, AddressLength);
            // Fixed: offset to topics (points past fixed section)
            writer.WriteUInt32((uint)fixedSize);
            // Fixed: offset to data (points past fixed section + topics)
            writer.WriteUInt32((uint)(fixedSize + 4 + topicsSize)); // +4 for topic count
            // Variable: topics — written as count + elements for List[Bytes32, N]
            writer.WriteUInt32((uint)topics.Count);
            foreach (var topic in topics)
                writer.WriteFixedBytes(topic, TopicLength);
            // Variable: data (ProgressiveByteList — length-prefixed)
            writer.WriteUInt32((uint)data.Length);
            writer.WriteBytes(data);

            return writer.ToArray();
        }

        public Log Decode(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);
            var address = reader.ReadFixedBytes(AddressLength);
            var topicsOffset = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            // Read topics
            var topicCount = (int)reader.ReadUInt32();
            var topics = new List<byte[]>(topicCount);
            for (var i = 0; i < topicCount; i++)
                topics.Add(reader.ReadFixedBytes(TopicLength));

            // Read data
            var dataLength = (int)reader.ReadUInt32();
            var logData = reader.ReadFixedBytes(dataLength);

            return new Log
            {
                Address = "0x" + address.ToHex(),
                Topics = topics,
                Data = logData
            };
        }

        public byte[] HashTreeRoot(Log log)
        {
            var addressRoot = SszHashTreeRootHelper.HashTreeRootAddress(log.Address);

            var topicRoots = new List<byte[]>();
            if (log.Topics != null)
            {
                foreach (var topic in log.Topics)
                    topicRoots.Add(SszHashTreeRootHelper.HashTreeRootBytes32(topic));
            }
            var topicsMerkle = SszMerkleizer.Merkleize(topicRoots, MaxTopicsPerLog);
            var topicsRoot = SszMerkleizer.MixInLength(topicsMerkle, (ulong)(log.Topics?.Count ?? 0));

            var dataRoot = SszHashTreeRootHelper.HashTreeRootProgressiveByteList(log.Data);

            return SszMerkleizer.Merkleize(new List<byte[]> { addressRoot, topicsRoot, dataRoot });
        }

        private static byte[] GetAddressBytes(string address)
        {
            if (string.IsNullOrEmpty(address))
                return new byte[AddressLength];
            return address.HexToByteArray();
        }
    }
}
