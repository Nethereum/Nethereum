using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{
    public class BlockchainProcessorExecutionSteps
    {
        public IProcessor<Block> BlockStepProcessor = new Processor<Block>();
        public IProcessor<TransactionVO> TransactionStepProcessor = new Processor<TransactionVO>();
        public IProcessor<TransactionReceiptVO> TransactionReceiptStepProcessor = new Processor<TransactionReceiptVO>();
        public IProcessor<FilterLogVO> FilterLogStepProcesor = new Processor<FilterLogVO>();
        public IProcessor<ContractCreationVO> ContractCreationStepProcessor = new Processor<ContractCreationVO>();
        public virtual IProcessor<T>  GetStep<T>()
        {
            var type = typeof(T);
            if (type == typeof(Block))
            {
                return (IProcessor<T>)BlockStepProcessor;
            }
            else if (type == typeof(TransactionVO))
            {
                return (IProcessor<T>)TransactionStepProcessor;
            }
            else if (type == typeof(TransactionReceiptVO))
            {
                return (IProcessor<T>)TransactionReceiptStepProcessor;
            }
            else if (type == typeof(FilterLogVO))
            {
                return (IProcessor<T>)FilterLogStepProcesor;
            }
            else if (type == typeof(ContractCreationVO))
            {
                return (IProcessor<T>)ContractCreationStepProcessor;
            }

            return null;
        }
    }
}