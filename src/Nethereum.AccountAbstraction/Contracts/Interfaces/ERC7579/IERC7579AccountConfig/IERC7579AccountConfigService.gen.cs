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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579AccountConfig.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579AccountConfig
{
    public partial class IERC7579AccountConfigService: IERC7579AccountConfigServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountConfigDeployment iERC7579AccountConfigDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579AccountConfigDeployment>().SendRequestAndWaitForReceiptAsync(iERC7579AccountConfigDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountConfigDeployment iERC7579AccountConfigDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IERC7579AccountConfigDeployment>().SendRequestAsync(iERC7579AccountConfigDeployment);
        }

        public static async Task<IERC7579AccountConfigService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IERC7579AccountConfigDeployment iERC7579AccountConfigDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iERC7579AccountConfigDeployment, cancellationTokenSource);
            return new IERC7579AccountConfigService(web3, receipt.ContractAddress);
        }

        public IERC7579AccountConfigService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IERC7579AccountConfigServiceBase: ContractWeb3ServiceBase
    {

        public IERC7579AccountConfigServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountIdFunction),
                typeof(SupportsExecutionModeFunction),
                typeof(SupportsModuleFunction)
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
