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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579ModuleConfig.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579ModuleConfig
{
    public partial class IERC7579ModuleConfigService: IERC7579ModuleConfigServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IERC7579ModuleConfigDeployment iERC7579ModuleConfigDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579ModuleConfigDeployment>().SendRequestAndWaitForReceiptAsync(iERC7579ModuleConfigDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IERC7579ModuleConfigDeployment iERC7579ModuleConfigDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579ModuleConfigDeployment>().SendRequestAsync(iERC7579ModuleConfigDeployment);
        }

        public static async Task<IERC7579ModuleConfigService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IERC7579ModuleConfigDeployment iERC7579ModuleConfigDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iERC7579ModuleConfigDeployment, cancellationTokenSource);
            return new IERC7579ModuleConfigService(web3, receipt.ContractAddress);
        }

        public IERC7579ModuleConfigService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IERC7579ModuleConfigServiceBase: ContractWeb3ServiceBase
    {

        public IERC7579ModuleConfigServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
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
                typeof(InstallModuleFunction),
                typeof(IsModuleInstalledFunction),
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
