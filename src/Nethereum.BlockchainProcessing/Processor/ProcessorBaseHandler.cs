using System;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public abstract class ProcessorBaseHandler<T> : IProcessorHandler<T>
    {
        public Func<T, Task<bool>> Criteria { get; protected set; }
        protected ProcessorBaseHandler()
        {
            
        }
        protected ProcessorBaseHandler(Func<T, Task<bool>> criteria)
        {
            SetMatchCriteria(criteria);
        }

        protected ProcessorBaseHandler(Func<T, bool> criteria)
        {
            SetMatchCriteria(criteria);
        }

        public void SetMatchCriteria(Func<T, bool> criteria)
        {
            Func<T, Task<bool>> asyncCriteria = async (t) => criteria(t);

            SetMatchCriteria(asyncCriteria);
        }
        public void SetMatchCriteria(Func<T, Task<bool>> criteria)
        {
            Criteria = criteria;
        }

        public virtual async Task<bool> IsMatchAsync(T value)
        {
            if (Criteria == null) return true;
            return await Criteria(value).ConfigureAwait(false);
        }

        public virtual async Task ExecuteAsync(T value)
        {
            if (await IsMatchAsync(value).ConfigureAwait(false))
            {
                await ExecuteInternalAsync(value).ConfigureAwait(false);
            }
        }
        protected abstract Task ExecuteInternalAsync(T value);

    }
}