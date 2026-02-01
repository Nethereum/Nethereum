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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute
{
    public partial class IAccountExecuteService: IAccountExecuteServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IAccountExecuteDeployment iAccountExecuteDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountExecuteDeployment>().SendRequestAndWaitForReceiptAsync(iAccountExecuteDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IAccountExecuteDeployment iAccountExecuteDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountExecuteDeployment>().SendRequestAsync(iAccountExecuteDeployment);
        }

        public static async Task<IAccountExecuteService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IAccountExecuteDeployment iAccountExecuteDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iAccountExecuteDeployment, cancellationTokenSource);
            return new IAccountExecuteService(web3, receipt.ContractAddress);
        }

        public IAccountExecuteService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IAccountExecuteServiceBase: ContractWeb3ServiceBase
    {

        public IAccountExecuteServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public virtual Task<string> ExecuteRequestAsync(string dest, BigInteger value, byte[] data)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Dest = dest;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(string dest, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Dest = dest;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(ExecuteBatchFunction executeBatchFunction)
        {
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(ExecuteBatchFunction executeBatchFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(List<string> dest, List<BigInteger> value, List<byte[]> data)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Dest = dest;
                executeBatchFunction.Value = value;
                executeBatchFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(List<string> dest, List<BigInteger> value, List<byte[]> data, CancellationTokenSource cancellationToken = null)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Dest = dest;
                executeBatchFunction.Value = value;
                executeBatchFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ExecuteFunction),
                typeof(ExecuteBatchFunction)
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
