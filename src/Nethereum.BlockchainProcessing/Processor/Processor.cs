using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public class Processor<T>: IProcessor<T>
    {
        public Func<T, Task<bool>> Criteria { get; protected set; }
        protected List<Func<T,Task>> ProcessorHandlers { get; set; } = new List<Func<T, Task>>();
        public virtual void SetMatchCriteria(Func<T, bool> criteria)
        {
            Func<T, Task<bool>> asyncCriteria = async (t) => criteria(t);

            SetMatchCriteria(asyncCriteria);
        }

        public virtual void SetMatchCriteria(Func<T, Task<bool>> criteria)
        {
            Criteria = criteria;
        }

        public virtual void AddProcessorHandler(Func<T,Task> action)
        {
            ProcessorHandlers.Add(action);
        }
        public virtual void AddProcessorHandler(IProcessorHandler<T> processorHandler)
        {
            ProcessorHandlers.Add(processorHandler.ExecuteAsync);
        }

        public virtual async Task<bool> IsMatchAsync(T value)
        {
            if (Criteria == null) return true;
            return await Criteria(value).ConfigureAwait(false);
        }

        public virtual async Task ExecuteAsync(T value)
        {
            if (await IsMatchAsync(value))
            {
                foreach (var x in ProcessorHandlers)
                {
                    await x(value).ConfigureAwait(false);
                }
            }
        }

    }
}