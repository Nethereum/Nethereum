using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface IFlatStateBatchWriter
    {
        Task ApplyBatchAsync(FlatStateBatch batch);
    }
}
