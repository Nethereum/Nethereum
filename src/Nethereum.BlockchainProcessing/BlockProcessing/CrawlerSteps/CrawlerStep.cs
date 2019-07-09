using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public abstract class CrawlerStep<TParentStep, TProcessStep>
    {
        //TODO: Disable step and / or handlers
        protected IEthApiContractService EthApi { get; }
        public CrawlerStep(
            IEthApiContractService ethApi
        )
        {
            EthApi = ethApi;
        }

        public abstract Task<TProcessStep> GetStepDataAsync(TParentStep parentStep);

        public virtual async Task<CrawlerStepCompleted<TProcessStep>> ExecuteStepAsync(TParentStep parentStep, IEnumerable<BlockchainProcessorExecutionSteps> executionStepsCollection)
        {
            var processStepValue = await GetStepDataAsync(parentStep);
            if (processStepValue == null) return null;
            var stepsToProcesss =
                await executionStepsCollection.FilterMatchingStepAsync(parentStep).ConfigureAwait(false);

            if (stepsToProcesss.Any())
            {
                await stepsToProcesss.ExecuteCurrentStepAsync(parentStep);
            }
            return new CrawlerStepCompleted<TProcessStep>(stepsToProcesss, processStepValue);

        }
    }
}