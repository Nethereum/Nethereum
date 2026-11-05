using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbHotBlockWindowSourceAdapter : IHotBlockWindowSource, IHotBlockWindowEvict
    {
        public const long EmptyWindowSentinel = -1;

        private readonly RocksDbManager _core;
        private readonly RocksDbHotBlockWindowStore _window;

        public RocksDbHotBlockWindowSourceAdapter(RocksDbManager coreManager, RocksDbHotBlockWindowStore window)
        {
            _core = coreManager ?? throw new ArgumentNullException(nameof(coreManager));
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public long HotTipNumber
        {
            get
            {
                var latest = _window.TryGetLatestNumber();
                return latest.HasValue ? (long)latest.Value : EmptyWindowSentinel;
            }
        }

        public long HotFloorNumber
        {
            get
            {
                var oldest = _window.TryGetOldestNumber();
                return oldest.HasValue ? (long)oldest.Value : EmptyWindowSentinel;
            }
        }

        public HotBlock ReadHotBlock(long blockNumber)
        {
            var data = _window.ReadForPromotion((ulong)blockNumber);
            if (data == null)
                throw new FreezerConsistencyException(
                    $"hot window has a gap at block {blockNumber}; promotion requires contiguous history");

            return new HotBlock(
                data.Header,
                data.BlockHash,
                new BlockBodyCluster(data.Transactions, DecodeUncles(data.Meta?.Uncles), DecodeWithdrawals(data.Meta?.Withdrawals)),
                data.Receipts.Select(r => r.Receipt).ToList(),
                data.BlockAccessList);
        }

        public void EvictAtOrBelow(long number)
        {
            using var batch = _core.CreateWriteBatch();
            _window.RangeDeleteAtOrBelow(batch, (ulong)number);
            _core.Write(batch);
        }

        private static IReadOnlyList<BlockHeader> DecodeUncles(byte[] unclesRlp)
        {
            var result = new List<BlockHeader>();
            if (unclesRlp == null || unclesRlp.Length == 0) return result;
            var reader = new RlpReader(unclesRlp);
            var scope = reader.ReadListHeader();
            while (!reader.IsAtEnd(scope))
            {
                var header = BlockHeaderEncoder.Current.Decode(reader);
                if (header != null) result.Add(header);
            }
            reader.EnsureEnd(scope);
            reader.EnsureEnd();
            return result;
        }

        private static IReadOnlyList<Withdrawal> DecodeWithdrawals(byte[] withdrawalsRlp)
        {
            if (withdrawalsRlp == null || withdrawalsRlp.Length == 0) return null;
            var result = new List<Withdrawal>();
            var reader = new RlpReader(withdrawalsRlp);
            var scope = reader.ReadListHeader();
            while (!reader.IsAtEnd(scope))
            {
                var withdrawal = WithdrawalEncoder.Current.Decode(reader);
                if (withdrawal != null) result.Add(withdrawal);
            }
            reader.EnsureEnd(scope);
            reader.EnsureEnd();
            return result;
        }
    }
}
