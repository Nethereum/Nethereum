using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class CrawlerStepCompleted<T>
    {
        public CrawlerStepCompleted(IEnumerable<BlockProcessingSteps> executedStepsCollection, T stepData)
        {
            ExecutedStepsCollection = executedStepsCollection;
            StepData = stepData;
        }

        public IEnumerable<BlockProcessingSteps> ExecutedStepsCollection { get; private set; }
        public T StepData { get; private set; }

    }
}