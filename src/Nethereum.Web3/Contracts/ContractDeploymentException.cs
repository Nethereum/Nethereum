using System;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3.Contracts
{
    public class ContractDeploymentException : Exception
    {
        public TransactionReceipt TransactionReceipt { get; set; }

        public ContractDeploymentException(string message, TransactionReceipt transactionReceipt):base(message)
        {
            TransactionReceipt = transactionReceipt;
        }
    }
}