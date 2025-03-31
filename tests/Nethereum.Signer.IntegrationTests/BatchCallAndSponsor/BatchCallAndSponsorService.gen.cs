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
using Nethereum.Signer.IntegrationTests.BatchCallAndSponsor.ContractDefinition;

namespace Nethereum.Signer.IntegrationTests.BatchCallAndSponsor
{
    public partial class BatchCallAndSponsorService: BatchCallAndSponsorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BatchCallAndSponsorDeployment batchCallAndSponsorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BatchCallAndSponsorDeployment>().SendRequestAndWaitForReceiptAsync(batchCallAndSponsorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BatchCallAndSponsorDeployment batchCallAndSponsorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BatchCallAndSponsorDeployment>().SendRequestAsync(batchCallAndSponsorDeployment);
        }

        public static async Task<BatchCallAndSponsorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BatchCallAndSponsorDeployment batchCallAndSponsorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, batchCallAndSponsorDeployment, cancellationTokenSource);
            return new BatchCallAndSponsorService(web3, receipt.ContractAddress);
        }

        public BatchCallAndSponsorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class BatchCallAndSponsorServiceBase: ContractWeb3ServiceBase
    {

        public BatchCallAndSponsorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> ExecuteRequestAsync(ExecuteFunction executeFunction)
        {
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(ExecuteFunction executeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(List<Call> calls)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Calls = calls;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(List<Call> calls, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Calls = calls;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(Execute1Function execute1Function)
        {
             return ContractHandler.SendRequestAsync(execute1Function);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(Execute1Function execute1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(execute1Function, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(List<Call> calls, byte[] signature)
        {
            var execute1Function = new Execute1Function();
                execute1Function.Calls = calls;
                execute1Function.Signature = signature;
            
             return ContractHandler.SendRequestAsync(execute1Function);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(List<Call> calls, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var execute1Function = new Execute1Function();
                execute1Function.Calls = calls;
                execute1Function.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(execute1Function, cancellationToken);
        }

        public Task<BigInteger> NonceQueryAsync(NonceFunction nonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(nonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ExecuteFunction),
                typeof(Execute1Function),
                typeof(NonceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(BatchExecutedEventDTO),
                typeof(CallExecutedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError)
            };
        }
    }
}
