using System.Threading.Tasks;

namespace Nethereum.Chain.TestData
{
    public interface ISyncVector
    {
        string Name { get; }
        int Version { get; }
        Task BuildAsync(IVectorChainDriver driver);
    }
}
