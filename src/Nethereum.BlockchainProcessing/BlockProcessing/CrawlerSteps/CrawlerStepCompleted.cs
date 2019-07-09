using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class CrawlerStepCompleted<T>
    {
        public CrawlerStepCompleted(IEnumerable<BlockchainProcessorExecutionSteps> executedStepsCollection, T stepData)
        {
            ExecutedStepsCollection = executedStepsCollection;
            StepData = stepData;
        }

        public IEnumerable<BlockchainProcessorExecutionSteps> ExecutedStepsCollection { get; private set; }
        public T StepData { get; private set; }

    }
}