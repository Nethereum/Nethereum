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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Account.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Account
{
    public partial class IERC7579AccountService: IERC7579AccountServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountDeployment iERC7579AccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579AccountDeployment>().SendRequestAndWaitForReceiptAsync(iERC7579AccountDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountDeployment iERC7579AccountDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579AccountDeployment>().SendRequestAsync(iERC7579AccountDeployment);
        }

        public static async Task<IERC7579AccountService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountDeployment iERC7579AccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iERC7579AccountDeployment, cancellationTokenSource);
            return new IERC7579AccountService(web3, receipt.ContractAddress);
        }

        public IERC7579AccountService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IERC7579AccountServiceBase: ContractWeb3ServiceBase
    {

        public IERC7579AccountServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> AccountIdQueryAsync(AccountIdFunction accountIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountIdFunction, string>(accountIdFunction, blockParameter);
        }

        
        public virtual Task<string> AccountIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountIdFunction, string>(null, blockParameter);
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

        public virtual Task<string> InstallModuleRequestAsync(InstallModuleFunction installModuleFunction)
        {
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public virtual Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(InstallModuleFunction installModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public virtual Task<string> InstallModuleRequestAsync(BigInteger moduleTypeId, string module, byte[] initData)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.ModuleTypeId = moduleTypeId;
                installModuleFunction.Module = module;
                installModuleFunction.InitData = initData;
            
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public virtual Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(BigInteger moduleTypeId, string module, byte[] initData, CancellationTokenSource cancellationToken = null)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.ModuleTypeId = moduleTypeId;
                installModuleFunction.Module = module;
                installModuleFunction.InitData = initData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public Task<bool> IsModuleInstalledQueryAsync(IsModuleInstalledFunction isModuleInstalledFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleInstalledFunction, bool>(isModuleInstalledFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleInstalledQueryAsync(BigInteger moduleTypeId, string module, byte[] additionalContext, BlockParameter blockParameter = null)
        {
            var isModuleInstalledFunction = new IsModuleInstalledFunction();
                isModuleInstalledFunction.ModuleTypeId = moduleTypeId;
                isModuleInstalledFunction.Module = module;
                isModuleInstalledFunction.AdditionalContext = additionalContext;
            
            return ContractHandler.QueryAsync<IsModuleInstalledFunction, bool>(isModuleInstalledFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureQueryAsync(IsValidSignatureFunction isValidSignatureFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureFunction, byte[]>(isValidSignatureFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureQueryAsync(byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureFunction = new IsValidSignatureFunction();
                isValidSignatureFunction.Hash = hash;
                isValidSignatureFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<IsValidSignatureFunction, byte[]>(isValidSignatureFunction, blockParameter);
        }

        public Task<bool> SupportsExecutionModeQueryAsync(SupportsExecutionModeFunction supportsExecutionModeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsExecutionModeFunction, bool>(supportsExecutionModeFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsExecutionModeQueryAsync(byte[] encodedMode, BlockParameter blockParameter = null)
        {
            var supportsExecutionModeFunction = new SupportsExecutionModeFunction();
                supportsExecutionModeFunction.EncodedMode = encodedMode;
            
            return ContractHandler.QueryAsync<SupportsExecutionModeFunction, bool>(supportsExecutionModeFunction, blockParameter);
        }

        public Task<bool> SupportsModuleQueryAsync(SupportsModuleFunction supportsModuleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsModuleFunction, bool>(supportsModuleFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsModuleQueryAsync(BigInteger moduleTypeId, BlockParameter blockParameter = null)
        {
            var supportsModuleFunction = new SupportsModuleFunction();
                supportsModuleFunction.ModuleTypeId = moduleTypeId;
            
            return ContractHandler.QueryAsync<SupportsModuleFunction, bool>(supportsModuleFunction, blockParameter);
        }

        public virtual Task<string> UninstallModuleRequestAsync(UninstallModuleFunction uninstallModuleFunction)
        {
             return ContractHandler.SendRequestAsync(uninstallModuleFunction);
        }

        public virtual Task<TransactionReceipt> UninstallModuleRequestAndWaitForReceiptAsync(UninstallModuleFunction uninstallModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uninstallModuleFunction, cancellationToken);
        }

        public virtual Task<string> UninstallModuleRequestAsync(BigInteger moduleTypeId, string module, byte[] deInitData)
        {
            var uninstallModuleFunction = new UninstallModuleFunction();
                uninstallModuleFunction.ModuleTypeId = moduleTypeId;
                uninstallModuleFunction.Module = module;
                uninstallModuleFunction.DeInitData = deInitData;
            
             return ContractHandler.SendRequestAsync(uninstallModuleFunction);
        }

        public virtual Task<TransactionReceipt> UninstallModuleRequestAndWaitForReceiptAsync(BigInteger moduleTypeId, string module, byte[] deInitData, CancellationTokenSource cancellationToken = null)
        {
            var uninstallModuleFunction = new UninstallModuleFunction();
                uninstallModuleFunction.ModuleTypeId = moduleTypeId;
                uninstallModuleFunction.Module = module;
                uninstallModuleFunction.DeInitData = deInitData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uninstallModuleFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountIdFunction),
                typeof(ExecuteFunction),
                typeof(ExecuteFromExecutorFunction),
                typeof(InstallModuleFunction),
                typeof(IsModuleInstalledFunction),
                typeof(IsValidSignatureFunction),
                typeof(SupportsExecutionModeFunction),
                typeof(SupportsModuleFunction),
                typeof(UninstallModuleFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ModuleInstalledEventDTO),
                typeof(ModuleUninstalledEventDTO)
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
