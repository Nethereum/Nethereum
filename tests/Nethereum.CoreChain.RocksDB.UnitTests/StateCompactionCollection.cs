using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class StateCompactionCollection
    {
        public const string Name = "State compaction stores";
    }
}
