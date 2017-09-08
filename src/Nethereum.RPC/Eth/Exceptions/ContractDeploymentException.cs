using System;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Exceptions
{
    public class ContractDeploymentException : Exception
    {
        public ContractDeploymentException(string message, TransactionReceipt transactionReceipt) : base(message)
        {
            TransactionReceipt = transactionReceipt;
        }

        public TransactionReceipt TransactionReceipt { get; set; }
    }
}