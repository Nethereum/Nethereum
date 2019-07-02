using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockProcessing.ValueObjects
{
    public class ContractCreationTransaction: TransactionWithReceipt
    {
        public ContractCreationTransaction()
        {
                
        }

        public ContractCreationTransaction(
            string contractAddress, 
            string code, 
            Transaction transaction, 
            TransactionReceipt transactionReceipt, 
            bool failedCreatingContract, 
            HexBigInteger blockTimestamp)
        {
            ContractAddress = contractAddress;
            Code = code;
            Transaction = transaction;
            TransactionReceipt = transactionReceipt;
            FailedCreatingContract = failedCreatingContract;
            BlockTimestamp = blockTimestamp;
        }

        public string ContractAddress { get; private set; }
        public string Code { get; private set; }
        public bool FailedCreatingContract { get; private set; }

        public virtual TDeploymentMessage DecodeToDeploymentMessage<TDeploymentMessage>()
            where TDeploymentMessage : ContractDeploymentMessage, new()
        {
            return Transaction?.DecodeTransactionToDeploymentMessage<TDeploymentMessage>();
        }
    }
}
