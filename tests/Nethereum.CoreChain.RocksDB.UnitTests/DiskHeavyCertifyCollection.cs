using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class DiskHeavyCertifyCollection
    {
        public const string Name = "Disk-heavy flat certify stores";
    }
}
