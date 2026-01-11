using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.RocksDB.Serialization
{
    public static class RocksDbSerializer
    {
        public static byte[] SerializeBlockHeader(BlockHeader header)
        {
            return BlockHeaderEncoder.Current.Encode(header);
        }

        public static BlockHeader DeserializeBlockHeader(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return BlockHeaderEncoder.Current.Decode(data);
        }

        public static byte[] SerializeAccount(Account account)
        {
            return AccountEncoder.Current.Encode(account);
        }

        public static Account DeserializeAccount(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return AccountEncoder.Current.Decode(data);
        }

        public static byte[] SerializeReceipt(Receipt receipt)
        {
            return ReceiptEncoder.Current.Encode(receipt);
        }

        public static Receipt DeserializeReceipt(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return ReceiptEncoder.Current.Decode(data);
        }

        public static byte[] SerializeLog(Log log)
        {
            return LogEncoder.Current.Encode(log);
        }

        public static Log DeserializeLog(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return LogEncoder.Current.Decode(data);
        }

        public static byte[] SerializeReceiptInfo(ReceiptInfo info)
        {
            var receiptData = ReceiptEncoder.Current.Encode(info.Receipt);
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(receiptData),
                RLP.RLP.EncodeElement(info.TxHash),
                RLP.RLP.EncodeElement(info.BlockHash),
                RLP.RLP.EncodeElement(info.BlockNumber.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(info.TransactionIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(info.GasUsed.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(info.ContractAddress?.HexToByteArray() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(info.EffectiveGasPrice.ToBytesForRLPEncoding())
            );
        }

        public static ReceiptInfo DeserializeReceiptInfo(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            return new ReceiptInfo
            {
                Receipt = ReceiptEncoder.Current.Decode(elements[0].RLPData),
                TxHash = elements[1].RLPData,
                BlockHash = elements[2].RLPData,
                BlockNumber = elements[3].RLPData.ToBigIntegerFromRLPDecoded(),
                TransactionIndex = (int)elements[4].RLPData.ToLongFromRLPDecoded(),
                GasUsed = elements[5].RLPData.ToBigIntegerFromRLPDecoded(),
                ContractAddress = elements[6].RLPData?.Length > 0 ? elements[6].RLPData.ToHex(true) : null,
                EffectiveGasPrice = elements[7].RLPData.ToBigIntegerFromRLPDecoded()
            };
        }

        public static byte[] SerializeTransactionLocation(TransactionLocation location)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(location.BlockHash),
                RLP.RLP.EncodeElement(location.BlockNumber.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(location.TransactionIndex.ToBytesForRLPEncoding())
            );
        }

        public static TransactionLocation DeserializeTransactionLocation(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            return new TransactionLocation
            {
                BlockHash = elements[0].RLPData,
                BlockNumber = elements[1].RLPData.ToBigIntegerFromRLPDecoded(),
                TransactionIndex = (int)elements[2].RLPData.ToLongFromRLPDecoded()
            };
        }

        public static byte[] SerializeFilteredLog(FilteredLog log)
        {
            var encodedTopics = new List<byte[]>();
            foreach (var topic in log.Topics)
            {
                encodedTopics.Add(RLP.RLP.EncodeElement(topic));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(log.Address?.HexToByteArray() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(log.Data ?? Array.Empty<byte>()),
                RLP.RLP.EncodeList(encodedTopics.ToArray()),
                RLP.RLP.EncodeElement(log.BlockHash),
                RLP.RLP.EncodeElement(log.BlockNumber.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(log.TransactionHash),
                RLP.RLP.EncodeElement(log.TransactionIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(log.LogIndex.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(new byte[] { log.Removed ? (byte)1 : (byte)0 })
            );
        }

        public static FilteredLog DeserializeFilteredLog(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            var log = new FilteredLog
            {
                Address = elements[0].RLPData?.Length > 0 ? elements[0].RLPData.ToHex(true) : null,
                Data = elements[1].RLPData,
                BlockHash = elements[3].RLPData,
                BlockNumber = elements[4].RLPData.ToBigIntegerFromRLPDecoded(),
                TransactionHash = elements[5].RLPData,
                TransactionIndex = (int)elements[6].RLPData.ToLongFromRLPDecoded(),
                LogIndex = (int)elements[7].RLPData.ToLongFromRLPDecoded(),
                Removed = elements[8].RLPData?.Length > 0 && elements[8].RLPData[0] == 1
            };

            var topicsCollection = (RLPCollection)elements[2];
            log.Topics = new List<byte[]>();
            foreach (var topic in topicsCollection)
            {
                log.Topics.Add(topic.RLPData);
            }

            return log;
        }

        public static byte[] SerializeFilterState(FilterState state)
        {
            var logFilterBytes = state.LogFilter != null
                ? SerializeLogFilter(state.LogFilter)
                : Array.Empty<byte>();

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(state.Id ?? "")),
                RLP.RLP.EncodeElement(((int)state.Type).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(logFilterBytes),
                RLP.RLP.EncodeElement(state.LastCheckedBlock.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(state.CreatedAt.Ticks.ToBytesForRLPEncoding())
            );
        }

        public static FilterState DeserializeFilterState(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            var state = new FilterState
            {
                Id = Encoding.UTF8.GetString(elements[0].RLPData ?? Array.Empty<byte>()),
                Type = (FilterType)(int)elements[1].RLPData.ToLongFromRLPDecoded(),
                LastCheckedBlock = elements[3].RLPData.ToBigIntegerFromRLPDecoded(),
                CreatedAt = new DateTime(elements[4].RLPData.ToLongFromRLPDecoded())
            };

            if (elements[2].RLPData?.Length > 0)
            {
                state.LogFilter = DeserializeLogFilter(elements[2].RLPData);
            }

            return state;
        }

        public static byte[] SerializeLogFilter(LogFilter filter)
        {
            var addressBytes = new List<byte[]>();
            foreach (var addr in filter.Addresses ?? new List<string>())
            {
                addressBytes.Add(RLP.RLP.EncodeElement(addr?.HexToByteArray() ?? Array.Empty<byte>()));
            }

            var topicsBytes = new List<byte[]>();
            foreach (var topicList in filter.Topics ?? new List<List<byte[]>>())
            {
                var innerTopics = new List<byte[]>();
                foreach (var topic in topicList ?? new List<byte[]>())
                {
                    innerTopics.Add(RLP.RLP.EncodeElement(topic));
                }
                topicsBytes.Add(RLP.RLP.EncodeList(innerTopics.ToArray()));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(filter.FromBlock?.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(filter.ToBlock?.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeList(addressBytes.ToArray()),
                RLP.RLP.EncodeList(topicsBytes.ToArray())
            );
        }

        public static LogFilter DeserializeLogFilter(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            var filter = new LogFilter
            {
                FromBlock = elements[0].RLPData?.Length > 0
                    ? elements[0].RLPData.ToBigIntegerFromRLPDecoded()
                    : null,
                ToBlock = elements[1].RLPData?.Length > 0
                    ? elements[1].RLPData.ToBigIntegerFromRLPDecoded()
                    : null
            };

            var addressCollection = (RLPCollection)elements[2];
            filter.Addresses = new List<string>();
            foreach (var addr in addressCollection)
            {
                if (addr.RLPData?.Length > 0)
                {
                    filter.Addresses.Add(addr.RLPData.ToHex(true));
                }
            }

            var topicsCollection = (RLPCollection)elements[3];
            filter.Topics = new List<List<byte[]>>();
            foreach (var topicList in topicsCollection)
            {
                var innerTopics = new List<byte[]>();
                var innerCollection = topicList as RLPCollection;
                if (innerCollection != null)
                {
                    foreach (var topic in innerCollection)
                    {
                        innerTopics.Add(topic.RLPData);
                    }
                }
                filter.Topics.Add(innerTopics);
            }

            return filter;
        }

        public static byte[] BigIntegerToBytes(BigInteger value)
        {
            return value.ToBytesForRLPEncoding();
        }

        public static BigInteger BytesToBigInteger(byte[] data)
        {
            if (data == null || data.Length == 0) return BigInteger.Zero;
            return data.ToBigIntegerFromRLPDecoded();
        }

        public static byte[] IntToBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        public static int BytesToInt(byte[] data)
        {
            if (data == null || data.Length < 4) return 0;
            return BitConverter.ToInt32(data, 0);
        }

        public static byte[] CombineKeys(params byte[][] keys)
        {
            var totalLength = 0;
            foreach (var key in keys)
            {
                totalLength += key?.Length ?? 0;
            }

            var result = new byte[totalLength];
            var offset = 0;
            foreach (var key in keys)
            {
                if (key != null)
                {
                    Buffer.BlockCopy(key, 0, result, offset, key.Length);
                    offset += key.Length;
                }
            }

            return result;
        }

        public static byte[] CreateLogKey(BigInteger blockNumber, int txIndex, int logIndex)
        {
            var blockBytes = blockNumber.ToByteArray();
            var paddedBlock = new byte[32];
            if (blockBytes.Length <= 32)
            {
                Buffer.BlockCopy(blockBytes, 0, paddedBlock, 32 - blockBytes.Length, blockBytes.Length);
            }

            var txBytes = BitConverter.GetBytes(txIndex);
            var logBytes = BitConverter.GetBytes(logIndex);

            return CombineKeys(paddedBlock, txBytes, logBytes);
        }
    }
}
