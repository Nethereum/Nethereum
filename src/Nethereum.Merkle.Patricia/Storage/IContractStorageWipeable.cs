
using Nethereum.Documentation;

namespace Nethereum.Merkle.Patricia.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Wipe a whole contract storage subtree on a path store")]
    public interface IContractStorageWipeable
    {
        void DeleteRange(byte[] owner);
    }
}
