using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public interface IProcessorHandler<T>
    {
        Task ExecuteAsync(T value);
    }
}