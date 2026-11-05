using System;
using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    internal static class PeerRlp
    {
        public const int HashLength = 32;

        public static uint ReadUInt32(RlpReader reader)
        {
            var value = reader.ReadUInt64();
            if (value > uint.MaxValue)
                throw new InvalidOperationException("RLP integer does not fit in 32 bits");
            return (uint)value;
        }

        public static void ReadReceiptBlocks(RlpReader reader, Func<RlpReader, Receipt> readReceipt,
            List<List<Receipt>> into)
        {
            var blocks = reader.ReadListHeader();
            while (!reader.IsAtEnd(blocks))
            {
                var block = reader.ReadListHeader();
                if (reader.CountItems(block) > PeerResponseBounds.MaxTransactionsPerBlock)
                    throw new InvalidOperationException("RLP receipts list holds more than " + PeerResponseBounds.MaxTransactionsPerBlock + " receipts");
                var receipts = new List<Receipt>();
                while (!reader.IsAtEnd(block))
                    receipts.Add(readReceipt(reader));
                reader.EnsureEnd(block);
                into.Add(receipts);
            }
            reader.EnsureEnd(blocks);
        }

        public static List<byte[]> ReadByteStrings(RlpReader reader, int maxItems = int.MaxValue)
        {
            var scope = reader.ReadListHeader();
            if (maxItems != int.MaxValue && reader.CountItems(scope) > maxItems)
                throw new InvalidOperationException("RLP list holds more than " + maxItems + " items");
            var items = new List<byte[]>();
            while (!reader.IsAtEnd(scope))
                items.Add(reader.ReadBytes());
            reader.EnsureEnd(scope);
            return items;
        }

        public static List<byte[]> ReadBlockAccessLists(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var lists = new List<byte[]>();
            while (!reader.IsAtEnd(scope))
                lists.Add(reader.ReadData());
            reader.EnsureEnd(scope);
            return lists;
        }

        public static void AddUnique(HashSet<string> seen, ISignedTransaction transaction)
        {
            if (!seen.Add(Convert.ToBase64String(transaction.Hash)))
                throw new InvalidOperationException("multiple copies of the same transaction hash");
        }

        public static List<byte[]> ReadEthBlockAccessLists(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var lists = new List<byte[]>();
            while (!reader.IsAtEnd(scope))
            {
                if (reader.PeekKind() != RlpKind.List)
                {
                    if (reader.ReadBytes().Length != 0)
                        throw new InvalidOperationException("RLP block access list entry is a string but not the empty unavailable marker");
                    lists.Add(RLP.RLP.EMPTY_BYTE_ARRAY);
                    continue;
                }
                var entry = reader.ReadRaw();
                var inner = new RlpReader(entry);
                var items = inner.ReadListHeader();
                while (!inner.IsAtEnd(items))
                    inner.Skip();
                inner.EnsureEnd(items);
                inner.EnsureEnd();
                lists.Add(entry);
            }
            reader.EnsureEnd(scope);
            return lists;
        }

        public static byte[][] ReadHashes(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            const int EncodedHashLength = HashLength + 1;
            if (scope.Length % EncodedHashLength != 0)
                throw new InvalidOperationException("RLP hash list does not hold whole 32-byte hashes");
            var hashes = new byte[scope.Length / EncodedHashLength][];
            for (var i = 0; i < hashes.Length; i++)
                hashes[i] = reader.ReadFixed(HashLength);
            reader.EnsureEnd(scope);
            return hashes;
        }
    }
}
