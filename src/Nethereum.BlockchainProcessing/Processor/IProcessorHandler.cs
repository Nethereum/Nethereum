using System;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public interface IProcessorHandler<T>
    {
        void SetMatchCriteria(Func<T, bool> criteria);
        void SetMatchCriteria(Func<T, Task<bool>> criteria);
        Task ExecuteAsync(T value);
        Task<bool> IsMatchAsync(T value);
    }
}