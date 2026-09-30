using System;
using System.Collections.Generic;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    internal static class FilterMapsTestSupport
    {
        public static readonly FilterMapsParams TestParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 1,
            logValuesPerMap: 3,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        public static string Address(byte seed)
        {
            var bytes = new byte[20];
            bytes[19] = seed;
            return bytes.ToHex(true);
        }

        public static byte[] Topic(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        public static Log MakeLog(byte addressSeed, params byte[] topicSeeds)
        {
            var topics = new List<byte[]>();
            foreach (var seed in topicSeeds)
                topics.Add(Topic(seed));
            return new Log { Address = Address(addressSeed), Data = null, Topics = topics };
        }

        public static string Address(ushort seed)
        {
            var bytes = new byte[20];
            bytes[18] = (byte)(seed >> 8);
            bytes[19] = (byte)seed;
            return bytes.ToHex(true);
        }

        public static IReadOnlyList<ushort> FindColumnCollidingAddressSeeds(long mapIndex, int count, FilterMapsParams p, int searchPool = 8192)
        {
            var byLayer0 = new Dictionary<int, List<ushort>>();

            for (var seed = 0; seed < searchPool; seed++)
            {
                var hash = LogValueHasher.AddressValue(Address((ushort)seed).HexToByteArray());
                var row0 = LogValueHasher.RowIndex(mapIndex, 0, hash, p);
                if (!byLayer0.TryGetValue(row0, out var list))
                {
                    list = new List<ushort>();
                    byLayer0[row0] = list;
                }
                list.Add((ushort)seed);

                if (list.Count < count)
                    continue;

                var found = FindLayer1Collision(mapIndex, list, count, p);
                if (found != null)
                    return found;
            }

            throw new InvalidOperationException(
                $"could not find {count} address seeds colliding at both layer 0 and layer 1 for map {mapIndex} within a pool of {searchPool}");
        }

        private static List<ushort> FindLayer1Collision(long mapIndex, List<ushort> row0Candidates, int count, FilterMapsParams p)
        {
            var byLayer1 = new Dictionary<int, List<ushort>>();
            foreach (var seed in row0Candidates)
            {
                var hash = LogValueHasher.AddressValue(Address(seed).HexToByteArray());
                var row1 = LogValueHasher.RowIndex(mapIndex, 1, hash, p);
                if (!byLayer1.TryGetValue(row1, out var sub))
                {
                    sub = new List<ushort>();
                    byLayer1[row1] = sub;
                }
                sub.Add(seed);

                if (sub.Count == count)
                    return sub;
            }
            return null;
        }
    }

    internal sealed class FakeFinalitySource : IFinalitySource
    {
        public long FinalizedBlockNumber { get; set; }
    }

    internal sealed class FakeChainView : IChainView
    {
        private readonly Dictionary<long, List<ReceiptForStorage>> _receipts = new Dictionary<long, List<ReceiptForStorage>>();
        public long HeadNumber { get; set; }

        public void SetBlock(long number, params Log[] logs)
        {
            var receipt = new ReceiptForStorage(new byte[] { 0x01 }, cumulativeGasUsed: 21000, logs);
            _receipts[number] = new List<ReceiptForStorage> { receipt };
            if (number > HeadNumber) HeadNumber = number;
        }

        public byte[] BlockId(long number) => System.BitConverter.GetBytes(number);

        public BlockHeader Header(long number) => null;

        public IReadOnlyList<ReceiptForStorage> Receipts(long number) =>
            _receipts.TryGetValue(number, out var r) ? r : new List<ReceiptForStorage>();
    }

    internal sealed class CorpusChainView : IChainView
    {
        private readonly Nethereum.CoreChain.Freezer.Codecs.ReceiptsItemCodec _codec = new Nethereum.CoreChain.Freezer.Codecs.ReceiptsItemCodec();

        public long HeadNumber { get; } =
            CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true) - 1;

        public byte[] BlockId(long number) => System.BitConverter.GetBytes(number);

        public BlockHeader Header(long number) => null;

        public IReadOnlyList<ReceiptForStorage> Receipts(long number) =>
            _codec.Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true, number));
    }
}
