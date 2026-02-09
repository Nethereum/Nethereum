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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Execution.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Execution
{
    public partial class IERC7579ExecutionService: IERC7579ExecutionServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IERC7579ExecutionDeployment iERC7579ExecutionDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579ExecutionDeployment>().SendRequestAndWaitForReceiptAsync(iERC7579ExecutionDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IERC7579ExecutionDeployment iERC7579ExecutionDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579ExecutionDeployment>().SendRequestAsync(iERC7579ExecutionDeployment);
        }

        public static async Task<IERC7579ExecutionService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IERC7579ExecutionDeployment iERC7579ExecutionDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iERC7579ExecutionDeployment, cancellationTokenSource);
            return new IERC7579ExecutionService(web3, receipt.ContractAddress);
        }

        public IERC7579ExecutionService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IERC7579ExecutionServiceBase: ContractWeb3ServiceBase
    {

        public IERC7579ExecutionServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public virtual Task<string> ExecuteRequestAsync(byte[] mode, byte[] executionCalldata)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Mode = mode;
                executeFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(byte[] mode, byte[] executionCalldata, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Mode = mode;
                executeFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteFromExecutorRequestAsync(ExecuteFromExecutorFunction executeFromExecutorFunction)
        {
             return ContractHandler.SendRequestAsync(executeFromExecutorFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteFromExecutorRequestAndWaitForReceiptAsync(ExecuteFromExecutorFunction executeFromExecutorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFromExecutorFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteFromExecutorRequestAsync(byte[] mode, byte[] executionCalldata)
        {
            var executeFromExecutorFunction = new ExecuteFromExecutorFunction();
                executeFromExecutorFunction.Mode = mode;
                executeFromExecutorFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAsync(executeFromExecutorFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteFromExecutorRequestAndWaitForReceiptAsync(byte[] mode, byte[] executionCalldata, CancellationTokenSource cancellationToken = null)
        {
            var executeFromExecutorFunction = new ExecuteFromExecutorFunction();
                executeFromExecutorFunction.Mode = mode;
                executeFromExecutorFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFromExecutorFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ExecuteFunction),
                typeof(ExecuteFromExecutorFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
