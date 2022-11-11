using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.ErrorTest.ErrorTest.ContractDefinition;

namespace Nethereum.ErrorTest.ErrorTest
{
    public partial class ErrorTestService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, ErrorTestDeployment errorTestDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ErrorTestDeployment>().SendRequestAndWaitForReceiptAsync(errorTestDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ErrorTestDeployment errorTestDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ErrorTestDeployment>().SendRequestAsync(errorTestDeployment);
        }

        public static async Task<ErrorTestService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, ErrorTestDeployment errorTestDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, errorTestDeployment, cancellationTokenSource);
            return new ErrorTestService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public ErrorTestService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> AddOwnerWithThresholdRequestAsync(AddOwnerWithThresholdFunction addOwnerWithThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(addOwnerWithThresholdFunction);
        }

        public Task<TransactionReceipt> AddOwnerWithThresholdRequestAndWaitForReceiptAsync(AddOwnerWithThresholdFunction addOwnerWithThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addOwnerWithThresholdFunction, cancellationToken);
        }

        public Task<string> AddOwnerWithThresholdRequestAsync(string owner, BigInteger threshold)
        {
            var addOwnerWithThresholdFunction = new AddOwnerWithThresholdFunction();
                addOwnerWithThresholdFunction.Owner = owner;
                addOwnerWithThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(addOwnerWithThresholdFunction);
        }

        public Task<TransactionReceipt> AddOwnerWithThresholdRequestAndWaitForReceiptAsync(string owner, BigInteger threshold, CancellationTokenSource cancellationToken = null)
        {
            var addOwnerWithThresholdFunction = new AddOwnerWithThresholdFunction();
                addOwnerWithThresholdFunction.Owner = owner;
                addOwnerWithThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addOwnerWithThresholdFunction, cancellationToken);
        }

        public Task<string> ApproveHashRequestAsync(ApproveHashFunction approveHashFunction)
        {
             return ContractHandler.SendRequestAsync(approveHashFunction);
        }

        public Task<TransactionReceipt> ApproveHashRequestAndWaitForReceiptAsync(ApproveHashFunction approveHashFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveHashFunction, cancellationToken);
        }

        public Task<string> ApproveHashRequestAsync(byte[] hashToApprove)
        {
            var approveHashFunction = new ApproveHashFunction();
                approveHashFunction.HashToApprove = hashToApprove;
            
             return ContractHandler.SendRequestAsync(approveHashFunction);
        }

        public Task<TransactionReceipt> ApproveHashRequestAndWaitForReceiptAsync(byte[] hashToApprove, CancellationTokenSource cancellationToken = null)
        {
            var approveHashFunction = new ApproveHashFunction();
                approveHashFunction.HashToApprove = hashToApprove;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveHashFunction, cancellationToken);
        }

        public Task<string> ChangeThresholdRequestAsync(ChangeThresholdFunction changeThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(changeThresholdFunction);
        }

        public Task<TransactionReceipt> ChangeThresholdRequestAndWaitForReceiptAsync(ChangeThresholdFunction changeThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeThresholdFunction, cancellationToken);
        }

        public Task<string> ChangeThresholdRequestAsync(BigInteger threshold)
        {
            var changeThresholdFunction = new ChangeThresholdFunction();
                changeThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(changeThresholdFunction);
        }

        public Task<TransactionReceipt> ChangeThresholdRequestAndWaitForReceiptAsync(BigInteger threshold, CancellationTokenSource cancellationToken = null)
        {
            var changeThresholdFunction = new ChangeThresholdFunction();
                changeThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeThresholdFunction, cancellationToken);
        }
    }
}
