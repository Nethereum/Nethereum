using System.Collections.Generic;

namespace Nethereum.Freezer
{
    public enum FreezerOpenMode
    {
        Append,
        ReadOnly,
    }

    public enum FreezerTailGroup
    {
        HeadersAndHashes,
        BlockData,
        Bal,
    }

    public sealed class FreezerTableConfig
    {
        public string Name { get; }
        public bool UseCompression { get; }
        public FreezerTailGroup TailGroup { get; }

        public FreezerTableConfig(string name, bool useCompression, FreezerTailGroup tailGroup)
        {
            Name = name;
            UseCompression = useCompression;
            TailGroup = tailGroup;
        }
    }

    public sealed class FreezerLayout
    {
        public string Directory { get; }
        public long MaxFileSize { get; }

        public FreezerTableConfig Headers { get; } =
            new FreezerTableConfig("headers", useCompression: true, FreezerTailGroup.HeadersAndHashes);
        public FreezerTableConfig Hashes { get; } =
            new FreezerTableConfig("hashes", useCompression: false, FreezerTailGroup.HeadersAndHashes);
        public FreezerTableConfig Bodies { get; } =
            new FreezerTableConfig("bodies", useCompression: true, FreezerTailGroup.BlockData);
        public FreezerTableConfig Receipts { get; } =
            new FreezerTableConfig("receipts", useCompression: true, FreezerTailGroup.BlockData);
        public FreezerTableConfig Bals { get; } =
            new FreezerTableConfig("bals", useCompression: true, FreezerTailGroup.Bal);

        public FreezerLayout(string directory, long maxFileSize = RollingDataFiles.DefaultMaxFileSize)
        {
            Directory = directory;
            MaxFileSize = maxFileSize;
        }

        public IReadOnlyList<FreezerTableConfig> Tables => new[] { Headers, Hashes, Bodies, Receipts, Bals };
    }
}
