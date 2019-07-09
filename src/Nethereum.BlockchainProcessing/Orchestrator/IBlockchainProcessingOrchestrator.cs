using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Orchestrator
{
    public interface IBlockchainProcessingOrchestrator
    {
        Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber);
    }
}