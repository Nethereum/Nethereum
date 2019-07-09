using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Processor;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{
    public static class BlockchainProcessorExecutionStepsExtensions
    {
        public static async Task<bool> HasAnyStepMatchAsync<T>(this IEnumerable<BlockchainProcessorExecutionSteps> list,
            T value)
        {
            foreach (var item in list)
            {
                if (await item.GetStep<T>().IsMatchAsync(value))
                    return true;
            }

            return false;
        }

        public static async Task<bool> IsStepMatchAsync<T>(this IEnumerable<IProcessor<T>> list, T value)
        {
            foreach (var item in list)
            {
                if (await item.IsMatchAsync(value))
                    return true;
            }

            return false;
        }

        public static IEnumerable<IProcessor<T>> GetAllSteps<T>(
            this IEnumerable<BlockchainProcessorExecutionSteps> list)
        {
            return list.Select(x => x.GetStep<T>());
        }

        public static async Task<IEnumerable<BlockchainProcessorExecutionSteps>> FilterMatchingStepAsync<T>(
            this IEnumerable<BlockchainProcessorExecutionSteps> list, T value)
        {
            var listResult = new List<BlockchainProcessorExecutionSteps>();
            foreach (var item in list)
            {
                if (await item.GetStep<T>().IsMatchAsync(value))
                    listResult.Add(item);
            }

            return listResult;
        }

        public static async Task ExecuteCurrentStepAsync<T>(
            this IEnumerable<BlockchainProcessorExecutionSteps> list, T value)
        {
            var steps = list.GetAllSteps<T>();
            foreach (var step in steps)
            {
                await step.ExecuteAsync(value);
            }
        }
    


}


}