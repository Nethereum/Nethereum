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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IRecoveryModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IRecoveryModule
{
    public partial class IRecoveryModuleService: IRecoveryModuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IRecoveryModuleDeployment iRecoveryModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IRecoveryModuleDeployment>().SendRequestAndWaitForReceiptAsync(iRecoveryModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IRecoveryModuleDeployment iRecoveryModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IRecoveryModuleDeployment>().SendRequestAsync(iRecoveryModuleDeployment);
        }

        public static async Task<IRecoveryModuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IRecoveryModuleDeployment iRecoveryModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iRecoveryModuleDeployment, cancellationTokenSource);
            return new IRecoveryModuleService(web3, receipt.ContractAddress);
        }

        public IRecoveryModuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IRecoveryModuleServiceBase: ContractWeb3ServiceBase
    {

        public IRecoveryModuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> ApproveRecoveryRequestAsync(ApproveRecoveryFunction approveRecoveryFunction)
        {
             return ContractHandler.SendRequestAsync(approveRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRecoveryRequestAndWaitForReceiptAsync(ApproveRecoveryFunction approveRecoveryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> ApproveRecoveryRequestAsync(byte[] recoveryId)
        {
            var approveRecoveryFunction = new ApproveRecoveryFunction();
                approveRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAsync(approveRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRecoveryRequestAndWaitForReceiptAsync(byte[] recoveryId, CancellationTokenSource cancellationToken = null)
        {
            var approveRecoveryFunction = new ApproveRecoveryFunction();
                approveRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> CancelRecoveryRequestAsync(CancelRecoveryFunction cancelRecoveryFunction)
        {
             return ContractHandler.SendRequestAsync(cancelRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> CancelRecoveryRequestAndWaitForReceiptAsync(CancelRecoveryFunction cancelRecoveryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> CancelRecoveryRequestAsync(byte[] recoveryId)
        {
            var cancelRecoveryFunction = new CancelRecoveryFunction();
                cancelRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAsync(cancelRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> CancelRecoveryRequestAndWaitForReceiptAsync(byte[] recoveryId, CancellationTokenSource cancellationToken = null)
        {
            var cancelRecoveryFunction = new CancelRecoveryFunction();
                cancelRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRecoveryRequestAsync(ExecuteRecoveryFunction executeRecoveryFunction)
        {
             return ContractHandler.SendRequestAsync(executeRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRecoveryRequestAndWaitForReceiptAsync(ExecuteRecoveryFunction executeRecoveryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRecoveryRequestAsync(byte[] recoveryId)
        {
            var executeRecoveryFunction = new ExecuteRecoveryFunction();
                executeRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAsync(executeRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRecoveryRequestAndWaitForReceiptAsync(byte[] recoveryId, CancellationTokenSource cancellationToken = null)
        {
            var executeRecoveryFunction = new ExecuteRecoveryFunction();
                executeRecoveryFunction.RecoveryId = recoveryId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeRecoveryFunction, cancellationToken);
        }

        public Task<BigInteger> GetRecoveryDelayQueryAsync(GetRecoveryDelayFunction getRecoveryDelayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRecoveryDelayFunction, BigInteger>(getRecoveryDelayFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRecoveryDelayQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRecoveryDelayFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<GetRecoveryRequestOutputDTO> GetRecoveryRequestQueryAsync(GetRecoveryRequestFunction getRecoveryRequestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecoveryRequestFunction, GetRecoveryRequestOutputDTO>(getRecoveryRequestFunction, blockParameter);
        }

        public virtual Task<GetRecoveryRequestOutputDTO> GetRecoveryRequestQueryAsync(byte[] recoveryId, BlockParameter blockParameter = null)
        {
            var getRecoveryRequestFunction = new GetRecoveryRequestFunction();
                getRecoveryRequestFunction.RecoveryId = recoveryId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecoveryRequestFunction, GetRecoveryRequestOutputDTO>(getRecoveryRequestFunction, blockParameter);
        }

        public Task<BigInteger> GetRequiredApprovalsQueryAsync(GetRequiredApprovalsFunction getRequiredApprovalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRequiredApprovalsFunction, BigInteger>(getRequiredApprovalsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRequiredApprovalsQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getRequiredApprovalsFunction = new GetRequiredApprovalsFunction();
                getRequiredApprovalsFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetRequiredApprovalsFunction, BigInteger>(getRequiredApprovalsFunction, blockParameter);
        }

        public virtual Task<string> InitiateRecoveryRequestAsync(InitiateRecoveryFunction initiateRecoveryFunction)
        {
             return ContractHandler.SendRequestAsync(initiateRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> InitiateRecoveryRequestAndWaitForReceiptAsync(InitiateRecoveryFunction initiateRecoveryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initiateRecoveryFunction, cancellationToken);
        }

        public virtual Task<string> InitiateRecoveryRequestAsync(string account, string newOwner)
        {
            var initiateRecoveryFunction = new InitiateRecoveryFunction();
                initiateRecoveryFunction.Account = account;
                initiateRecoveryFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(initiateRecoveryFunction);
        }

        public virtual Task<TransactionReceipt> InitiateRecoveryRequestAndWaitForReceiptAsync(string account, string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var initiateRecoveryFunction = new InitiateRecoveryFunction();
                initiateRecoveryFunction.Account = account;
                initiateRecoveryFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initiateRecoveryFunction, cancellationToken);
        }

        public Task<bool> IsApproverQueryAsync(IsApproverFunction isApproverFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApproverFunction, bool>(isApproverFunction, blockParameter);
        }

        
        public virtual Task<bool> IsApproverQueryAsync(string account, string approver, BlockParameter blockParameter = null)
        {
            var isApproverFunction = new IsApproverFunction();
                isApproverFunction.Account = account;
                isApproverFunction.Approver = approver;
            
            return ContractHandler.QueryAsync<IsApproverFunction, bool>(isApproverFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ApproveRecoveryFunction),
                typeof(CancelRecoveryFunction),
                typeof(ExecuteRecoveryFunction),
                typeof(GetRecoveryDelayFunction),
                typeof(GetRecoveryRequestFunction),
                typeof(GetRequiredApprovalsFunction),
                typeof(InitiateRecoveryFunction),
                typeof(IsApproverFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(RecoveryApprovedEventDTO),
                typeof(RecoveryCancelledEventDTO),
                typeof(RecoveryExecutedEventDTO),
                typeof(RecoveryInitiatedEventDTO)
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
