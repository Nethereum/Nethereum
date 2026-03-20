using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;

namespace Nethereum.Model.SSZ
{
    // EIP-6466: SSZ Receipt types
    // BasicReceipt, CreateReceipt, SetCodeReceipt as ProgressiveContainers
    // Receipt as CompatibleUnion wrapping them
    public class SszReceiptEncoder
    {
        public const byte SelectorBasicReceipt = 0x01;
        public const byte SelectorCreateReceipt = 0x02;
        public const byte SelectorSetCodeReceipt = 0x03;
        public const int AddressLength = 20;

        public static readonly bool[] BasicReceiptActiveFields = { true, true, false, true, true };
        public static readonly bool[] CreateReceiptActiveFields = { true, true, true, true, true };
        public static readonly bool[] SetCodeReceiptActiveFields = { true, true, false, true, true, true };

        public static readonly SszReceiptEncoder Current = new SszReceiptEncoder();

        // --- Encode ---

        public byte[] EncodeBasicReceipt(string from, ulong gasUsed, List<Log> logs, bool status)
        {
            using var writer = new SszWriter();
            // ProgressiveContainer serialisation is identical to Container per EIP-7495
            // Fixed: from(20) + gas_used(8) + status(1) + logs_offset(4)
            var fixedSize = AddressLength + 8 + 1 + 4;
            writer.WriteFixedBytes(GetAddressBytes(from), AddressLength);
            writer.WriteUInt64(gasUsed);
            writer.WriteUInt32((uint)fixedSize); // offset to logs
            writer.WriteBoolean(status);
            // Variable: logs
            WriteLogList(writer, logs);
            return writer.ToArray();
        }

        public byte[] EncodeCreateReceipt(string from, ulong gasUsed, string contractAddress,
            List<Log> logs, bool status)
        {
            using var writer = new SszWriter();
            var fixedSize = AddressLength + 8 + AddressLength + 1 + 4;
            writer.WriteFixedBytes(GetAddressBytes(from), AddressLength);
            writer.WriteUInt64(gasUsed);
            writer.WriteFixedBytes(GetAddressBytes(contractAddress), AddressLength);
            writer.WriteUInt32((uint)fixedSize); // offset to logs
            writer.WriteBoolean(status);
            WriteLogList(writer, logs);
            return writer.ToArray();
        }

        public byte[] EncodeReceipt(byte selector, byte[] receiptData)
        {
            // CompatibleUnion: [selector_byte][data]
            var result = new byte[1 + receiptData.Length];
            result[0] = selector;
            Buffer.BlockCopy(receiptData, 0, result, 1, receiptData.Length);
            return result;
        }

        // --- Decode ---

        public void DecodeBasicReceipt(ReadOnlySpan<byte> data,
            out string from, out ulong gasUsed, out List<Log> logs, out bool status)
        {
            var reader = new SszReader(data);
            from = "0x" + reader.ReadFixedBytes(AddressLength).ToHex();
            gasUsed = reader.ReadUInt64();
            var logsOffset = reader.ReadUInt32();
            status = reader.ReadBoolean();
            logs = ReadLogList(ref reader);
        }

        public void DecodeCreateReceipt(ReadOnlySpan<byte> data,
            out string from, out ulong gasUsed, out string contractAddress,
            out List<Log> logs, out bool status)
        {
            var reader = new SszReader(data);
            from = "0x" + reader.ReadFixedBytes(AddressLength).ToHex();
            gasUsed = reader.ReadUInt64();
            contractAddress = "0x" + reader.ReadFixedBytes(AddressLength).ToHex();
            var logsOffset = reader.ReadUInt32();
            status = reader.ReadBoolean();
            logs = ReadLogList(ref reader);
        }

        public byte DecodeReceiptSelector(ReadOnlySpan<byte> data)
        {
            return data[0];
        }

        public ReadOnlySpan<byte> DecodeReceiptData(ReadOnlySpan<byte> data)
        {
            return data.Slice(1);
        }

        // --- HashTreeRoot ---

        public byte[] HashTreeRootBasicReceipt(string from, ulong gasUsed, List<Log> logs, bool status)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootAddress(from),
                SszHashTreeRootHelper.HashTreeRootUint64(gasUsed),
                HashTreeRootLogList(logs),
                SszHashTreeRootHelper.HashTreeRootBoolean(status)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, BasicReceiptActiveFields);
        }

        public byte[] HashTreeRootCreateReceipt(string from, ulong gasUsed, string contractAddress,
            List<Log> logs, bool status)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootAddress(from),
                SszHashTreeRootHelper.HashTreeRootUint64(gasUsed),
                SszHashTreeRootHelper.HashTreeRootAddress(contractAddress),
                HashTreeRootLogList(logs),
                SszHashTreeRootHelper.HashTreeRootBoolean(status)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, CreateReceiptActiveFields);
        }

        public byte[] HashTreeRootSetCodeReceipt(string from, ulong gasUsed, List<Log> logs,
            bool status, List<string> authorities)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootAddress(from),
                SszHashTreeRootHelper.HashTreeRootUint64(gasUsed),
                HashTreeRootLogList(logs),
                SszHashTreeRootHelper.HashTreeRootBoolean(status),
                HashTreeRootAddressList(authorities)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, SetCodeReceiptActiveFields);
        }

        public byte[] HashTreeRootReceipt(byte selector, byte[] dataRoot)
        {
            return SszMerkleizer.HashTreeRootCompatibleUnion(dataRoot, selector);
        }

        public byte[] HashTreeRootReceiptsRoot(List<byte[]> receiptRoots)
        {
            return SszMerkleizer.HashTreeRootProgressiveList(receiptRoots);
        }

        // --- Helpers ---

        private void WriteLogList(SszWriter writer, List<Log> logs)
        {
            var logEncoder = SszLogEncoder.Current;
            writer.WriteUInt32((uint)(logs?.Count ?? 0));
            if (logs != null)
            {
                foreach (var log in logs)
                {
                    var encoded = logEncoder.Encode(log);
                    writer.WriteUInt32((uint)encoded.Length);
                    writer.WriteBytes(encoded);
                }
            }
        }

        private List<Log> ReadLogList(ref SszReader reader)
        {
            var count = (int)reader.ReadUInt32();
            var logs = new List<Log>(count);
            var logEncoder = SszLogEncoder.Current;
            for (var i = 0; i < count; i++)
            {
                var logLength = (int)reader.ReadUInt32();
                var logData = reader.ReadFixedBytes(logLength);
                logs.Add(logEncoder.Decode(logData));
            }
            return logs;
        }

        private byte[] HashTreeRootLogList(List<Log> logs)
        {
            var roots = new List<byte[]>();
            if (logs != null)
                foreach (var log in logs)
                    roots.Add(SszLogEncoder.Current.HashTreeRoot(log));
            return SszMerkleizer.HashTreeRootProgressiveList(roots);
        }

        private byte[] HashTreeRootAddressList(List<string> addresses)
        {
            var roots = new List<byte[]>();
            if (addresses != null)
                foreach (var addr in addresses)
                    roots.Add(SszHashTreeRootHelper.HashTreeRootAddress(addr));
            return SszMerkleizer.HashTreeRootProgressiveList(roots);
        }

        private static byte[] GetAddressBytes(string address)
        {
            if (string.IsNullOrEmpty(address)) return new byte[AddressLength];
            return address.HexToByteArray();
        }
    }
}
