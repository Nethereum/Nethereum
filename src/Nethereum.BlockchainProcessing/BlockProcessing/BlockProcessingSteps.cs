using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{
    public class BlockProcessingSteps
    {
        public IProcessor<Block> BlockStep = new Processor<Block>();
        public IProcessor<TransactionVO> TransactionStep = new Processor<TransactionVO>();
        public IProcessor<TransactionReceiptVO> TransactionReceiptStep = new Processor<TransactionReceiptVO>();
        public IProcessor<FilterLogVO> FilterLogStep = new Processor<FilterLogVO>();
        public IProcessor<ContractCreationVO> ContractCreationStep = new Processor<ContractCreationVO>();
        public virtual IProcessor<T>  GetStep<T>()
        {
            var type = typeof(T);
            if (type == typeof(Block))
            {
                return (IProcessor<T>)BlockStep;
            }
            else if (type == typeof(TransactionVO))
            {
                return (IProcessor<T>)TransactionStep;
            }
            else if (type == typeof(TransactionReceiptVO))
            {
                return (IProcessor<T>)TransactionReceiptStep;
            }
            else if (type == typeof(FilterLogVO))
            {
                return (IProcessor<T>)FilterLogStep;
            }
            else if (type == typeof(ContractCreationVO))
            {
                return (IProcessor<T>)ContractCreationStep;
            }

            return null;
        }
    }
}