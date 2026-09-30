namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public readonly struct LogValueEntry
    {
        public long BlockNumber { get; }
        public long LvIndex { get; }
        public byte[] ValueHash { get; }
        public bool IsBlockDelimiter { get; }

        private LogValueEntry(long blockNumber, long lvIndex, byte[] valueHash, bool isBlockDelimiter)
        {
            BlockNumber = blockNumber;
            LvIndex = lvIndex;
            ValueHash = valueHash;
            IsBlockDelimiter = isBlockDelimiter;
        }

        public static LogValueEntry Value(long blockNumber, long lvIndex, byte[] valueHash) =>
            new LogValueEntry(blockNumber, lvIndex, valueHash, isBlockDelimiter: false);

        public static LogValueEntry Delimiter(long blockNumber, long lvIndex) =>
            new LogValueEntry(blockNumber, lvIndex, null, isBlockDelimiter: true);
    }
}
