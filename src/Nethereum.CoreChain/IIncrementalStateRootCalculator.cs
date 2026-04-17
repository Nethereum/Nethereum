using System.Threading.Tasks;

namespace Nethereum.CoreChain
{
    public interface IIncrementalStateRootCalculator
    {
        Task<byte[]> ComputeStateRootAsync();
        Task<byte[]> ComputeFullStateRootAsync();
        void Reset();
    }
}
