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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator
{
    public partial class ECDSASessionValidatorService: ECDSASessionValidatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ECDSASessionValidatorDeployment eCDSASessionValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ECDSASessionValidatorDeployment>().SendRequestAndWaitForReceiptAsync(eCDSASessionValidatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ECDSASessionValidatorDeployment eCDSASessionValidatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ECDSASessionValidatorDeployment>().SendRequestAsync(eCDSASessionValidatorDeployment);
        }

        public static async Task<ECDSASessionValidatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ECDSASessionValidatorDeployment eCDSASessionValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eCDSASessionValidatorDeployment, cancellationTokenSource);
            return new ECDSASessionValidatorService(web3, receipt.ContractAddress);
        }

        public ECDSASessionValidatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ECDSASessionValidatorServiceBase: ContractWeb3ServiceBase
    {

        public ECDSASessionValidatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var isInitializedFunction = new IsInitializedFunction();
                isInitializedFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        public Task<bool> IsModuleTypeQueryAsync(IsModuleTypeFunction isModuleTypeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleTypeQueryAsync(BigInteger moduleTypeId, BlockParameter blockParameter = null)
        {
            var isModuleTypeFunction = new IsModuleTypeFunction();
                isModuleTypeFunction.ModuleTypeId = moduleTypeId;
            
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }





        public Task<bool> ValidateSignatureWithDataQueryAsync(ValidateSignatureWithDataFunction validateSignatureWithDataFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
        }

        
        public virtual Task<bool> ValidateSignatureWithDataQueryAsync(byte[] hash, byte[] sig, byte[] data, BlockParameter blockParameter = null)
        {
            var validateSignatureWithDataFunction = new ValidateSignatureWithDataFunction();
                validateSignatureWithDataFunction.Hash = hash;
                validateSignatureWithDataFunction.Sig = sig;
                validateSignatureWithDataFunction.Data = data;
            
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(ValidateSignatureWithDataFunction)
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
                typeof(AlreadyInitializedError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(InvalidSessionKeyDataError),
                typeof(NotInitializedError)
            };
        }
    }
}
