using System.IO;

namespace Nethereum.Freezer
{
    public sealed class FreezerTablePaths
    {
        public string Directory { get; }
        public string Name { get; }
        public bool UseCompression { get; }

        public FreezerTablePaths(string directory, string name, bool useCompression)
        {
            Directory = directory;
            Name = name;
            UseCompression = useCompression;
        }

        public string IndexPath => Path.Combine(Directory, $"{Name}.{(UseCompression ? "cidx" : "ridx")}");
        public string MetaPath => Path.Combine(Directory, $"{Name}.meta");
    }
}
