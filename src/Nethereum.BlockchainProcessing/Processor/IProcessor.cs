using System;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public interface IProcessor<T>: IProcessorHandler<T>
    {
        void AddProcessorHandler(Func<T, Task> action);
        void AddProcessorHandler(IProcessorHandler<T> processorHandler);
    }
}