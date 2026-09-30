namespace Nethereum.CoreChain.RocksDB.Stores
{
    public interface INodeHistoryFloorPolicy
    {
        ulong FloorFor(ulong head);
    }

    public sealed class FixedWindowFloorPolicy : INodeHistoryFloorPolicy
    {
        private readonly int _nodeHistoryBlocks;

        public FixedWindowFloorPolicy(int nodeHistoryBlocks)
        {
            _nodeHistoryBlocks = nodeHistoryBlocks;
        }

        public ulong FloorFor(ulong head)
        {
            if (_nodeHistoryBlocks <= 0) return 0;
            var window = (ulong)_nodeHistoryBlocks;
            return head > window ? head - window : 0;
        }
    }
}
