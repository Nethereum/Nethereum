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
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator
{
    public partial class WebAuthnValidatorService: WebAuthnValidatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, WebAuthnValidatorDeployment webAuthnValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<WebAuthnValidatorDeployment>().SendRequestAndWaitForReceiptAsync(webAuthnValidatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, WebAuthnValidatorDeployment webAuthnValidatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<WebAuthnValidatorDeployment>().SendRequestAsync(webAuthnValidatorDeployment);
        }

        public static async Task<WebAuthnValidatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, WebAuthnValidatorDeployment webAuthnValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, webAuthnValidatorDeployment, cancellationTokenSource);
            return new WebAuthnValidatorService(web3, receipt.ContractAddress);
        }

        public WebAuthnValidatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class WebAuthnValidatorServiceBase: ContractWeb3ServiceBase
    {

        public WebAuthnValidatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddCredentialRequestAsync(AddCredentialFunction addCredentialFunction)
        {
             return ContractHandler.SendRequestAsync(addCredentialFunction);
        }

        public virtual Task<TransactionReceipt> AddCredentialRequestAndWaitForReceiptAsync(AddCredentialFunction addCredentialFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addCredentialFunction, cancellationToken);
        }

        public virtual Task<string> AddCredentialRequestAsync(BigInteger pubKeyX, BigInteger pubKeyY, bool requireUV)
        {
            var addCredentialFunction = new AddCredentialFunction();
                addCredentialFunction.PubKeyX = pubKeyX;
                addCredentialFunction.PubKeyY = pubKeyY;
                addCredentialFunction.RequireUV = requireUV;
            
             return ContractHandler.SendRequestAsync(addCredentialFunction);
        }

        public virtual Task<TransactionReceipt> AddCredentialRequestAndWaitForReceiptAsync(BigInteger pubKeyX, BigInteger pubKeyY, bool requireUV, CancellationTokenSource cancellationToken = null)
        {
            var addCredentialFunction = new AddCredentialFunction();
                addCredentialFunction.PubKeyX = pubKeyX;
                addCredentialFunction.PubKeyY = pubKeyY;
                addCredentialFunction.RequireUV = requireUV;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addCredentialFunction, cancellationToken);
        }

        public virtual Task<CredentialDetailsOutputDTO> CredentialDetailsQueryAsync(CredentialDetailsFunction credentialDetailsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<CredentialDetailsFunction, CredentialDetailsOutputDTO>(credentialDetailsFunction, blockParameter);
        }

        public virtual Task<CredentialDetailsOutputDTO> CredentialDetailsQueryAsync(byte[] credentialId, string account, BlockParameter blockParameter = null)
        {
            var credentialDetailsFunction = new CredentialDetailsFunction();
                credentialDetailsFunction.CredentialId = credentialId;
                credentialDetailsFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<CredentialDetailsFunction, CredentialDetailsOutputDTO>(credentialDetailsFunction, blockParameter);
        }

        public Task<byte[]> GenerateCredentialIdQueryAsync(GenerateCredentialIdFunction generateCredentialIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GenerateCredentialIdFunction, byte[]>(generateCredentialIdFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GenerateCredentialIdQueryAsync(BigInteger pubKeyX, BigInteger pubKeyY, BlockParameter blockParameter = null)
        {
            var generateCredentialIdFunction = new GenerateCredentialIdFunction();
                generateCredentialIdFunction.PubKeyX = pubKeyX;
                generateCredentialIdFunction.PubKeyY = pubKeyY;
            
            return ContractHandler.QueryAsync<GenerateCredentialIdFunction, byte[]>(generateCredentialIdFunction, blockParameter);
        }

        public Task<BigInteger> GetCredentialCountQueryAsync(GetCredentialCountFunction getCredentialCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetCredentialCountFunction, BigInteger>(getCredentialCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetCredentialCountQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getCredentialCountFunction = new GetCredentialCountFunction();
                getCredentialCountFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetCredentialCountFunction, BigInteger>(getCredentialCountFunction, blockParameter);
        }

        public Task<List<byte[]>> GetCredentialIdsQueryAsync(GetCredentialIdsFunction getCredentialIdsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetCredentialIdsFunction, List<byte[]>>(getCredentialIdsFunction, blockParameter);
        }

        
        public virtual Task<List<byte[]>> GetCredentialIdsQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getCredentialIdsFunction = new GetCredentialIdsFunction();
                getCredentialIdsFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetCredentialIdsFunction, List<byte[]>>(getCredentialIdsFunction, blockParameter);
        }

        public virtual Task<GetCredentialInfoOutputDTO> GetCredentialInfoQueryAsync(GetCredentialInfoFunction getCredentialInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetCredentialInfoFunction, GetCredentialInfoOutputDTO>(getCredentialInfoFunction, blockParameter);
        }

        public virtual Task<GetCredentialInfoOutputDTO> GetCredentialInfoQueryAsync(byte[] credentialId, string account, BlockParameter blockParameter = null)
        {
            var getCredentialInfoFunction = new GetCredentialInfoFunction();
                getCredentialInfoFunction.CredentialId = credentialId;
                getCredentialInfoFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetCredentialInfoFunction, GetCredentialInfoOutputDTO>(getCredentialInfoFunction, blockParameter);
        }

        public Task<bool> HasCredentialQueryAsync(HasCredentialFunction hasCredentialFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HasCredentialFunction, bool>(hasCredentialFunction, blockParameter);
        }

        
        public virtual Task<bool> HasCredentialQueryAsync(BigInteger pubKeyX, BigInteger pubKeyY, string account, BlockParameter blockParameter = null)
        {
            var hasCredentialFunction = new HasCredentialFunction();
                hasCredentialFunction.PubKeyX = pubKeyX;
                hasCredentialFunction.PubKeyY = pubKeyY;
                hasCredentialFunction.Account = account;
            
            return ContractHandler.QueryAsync<HasCredentialFunction, bool>(hasCredentialFunction, blockParameter);
        }

        public Task<bool> HasCredentialByIdQueryAsync(HasCredentialByIdFunction hasCredentialByIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HasCredentialByIdFunction, bool>(hasCredentialByIdFunction, blockParameter);
        }

        
        public virtual Task<bool> HasCredentialByIdQueryAsync(byte[] credentialId, string account, BlockParameter blockParameter = null)
        {
            var hasCredentialByIdFunction = new HasCredentialByIdFunction();
                hasCredentialByIdFunction.CredentialId = credentialId;
                hasCredentialByIdFunction.Account = account;
            
            return ContractHandler.QueryAsync<HasCredentialByIdFunction, bool>(hasCredentialByIdFunction, blockParameter);
        }

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(string smartAccount, BlockParameter blockParameter = null)
        {
            var isInitializedFunction = new IsInitializedFunction();
                isInitializedFunction.SmartAccount = smartAccount;
            
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        public Task<bool> IsModuleTypeQueryAsync(IsModuleTypeFunction isModuleTypeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleTypeQueryAsync(BigInteger typeID, BlockParameter blockParameter = null)
        {
            var isModuleTypeFunction = new IsModuleTypeFunction();
                isModuleTypeFunction.TypeID = typeID;
            
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureWithSenderQueryAsync(IsValidSignatureWithSenderFunction isValidSignatureWithSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string returnValue1, byte[] hash, byte[] data, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.ReturnValue1 = returnValue1;
                isValidSignatureWithSenderFunction.Hash = hash;
                isValidSignatureWithSenderFunction.Data = data;
            
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public virtual Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public virtual Task<string> OnInstallRequestAsync(OnInstallFunction onInstallFunction)
        {
             return ContractHandler.SendRequestAsync(onInstallFunction);
        }

        public virtual Task<TransactionReceipt> OnInstallRequestAndWaitForReceiptAsync(OnInstallFunction onInstallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onInstallFunction, cancellationToken);
        }

        public virtual Task<string> OnInstallRequestAsync(byte[] data)
        {
            var onInstallFunction = new OnInstallFunction();
                onInstallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(onInstallFunction);
        }

        public virtual Task<TransactionReceipt> OnInstallRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var onInstallFunction = new OnInstallFunction();
                onInstallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onInstallFunction, cancellationToken);
        }

        public virtual Task<string> OnUninstallRequestAsync(OnUninstallFunction onUninstallFunction)
        {
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(OnUninstallFunction onUninstallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onUninstallFunction, cancellationToken);
        }

        public virtual Task<string> OnUninstallRequestAsync(byte[] returnValue1)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.ReturnValue1 = returnValue1;
            
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(byte[] returnValue1, CancellationTokenSource cancellationToken = null)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.ReturnValue1 = returnValue1;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onUninstallFunction, cancellationToken);
        }

        public virtual Task<string> RemoveCredentialRequestAsync(RemoveCredentialFunction removeCredentialFunction)
        {
             return ContractHandler.SendRequestAsync(removeCredentialFunction);
        }

        public virtual Task<TransactionReceipt> RemoveCredentialRequestAndWaitForReceiptAsync(RemoveCredentialFunction removeCredentialFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeCredentialFunction, cancellationToken);
        }

        public virtual Task<string> RemoveCredentialRequestAsync(BigInteger pubKeyX, BigInteger pubKeyY)
        {
            var removeCredentialFunction = new RemoveCredentialFunction();
                removeCredentialFunction.PubKeyX = pubKeyX;
                removeCredentialFunction.PubKeyY = pubKeyY;
            
             return ContractHandler.SendRequestAsync(removeCredentialFunction);
        }

        public virtual Task<TransactionReceipt> RemoveCredentialRequestAndWaitForReceiptAsync(BigInteger pubKeyX, BigInteger pubKeyY, CancellationTokenSource cancellationToken = null)
        {
            var removeCredentialFunction = new RemoveCredentialFunction();
                removeCredentialFunction.PubKeyX = pubKeyX;
                removeCredentialFunction.PubKeyY = pubKeyY;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeCredentialFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(SetThresholdFunction setThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(SetThresholdFunction setThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(BigInteger threshold)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(BigInteger threshold, CancellationTokenSource cancellationToken = null)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public Task<BigInteger> ThresholdQueryAsync(ThresholdFunction thresholdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(thresholdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ThresholdQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var thresholdFunction = new ThresholdFunction();
                thresholdFunction.Account = account;
            
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(thresholdFunction, blockParameter);
        }

        public Task<bool> ValidateSignatureWithDataQueryAsync(ValidateSignatureWithDataFunction validateSignatureWithDataFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
        }

        
        public virtual Task<bool> ValidateSignatureWithDataQueryAsync(byte[] hash, byte[] signature, byte[] data, BlockParameter blockParameter = null)
        {
            var validateSignatureWithDataFunction = new ValidateSignatureWithDataFunction();
                validateSignatureWithDataFunction.Hash = hash;
                validateSignatureWithDataFunction.Signature = signature;
                validateSignatureWithDataFunction.Data = data;
            
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
        }

        public Task<BigInteger> ValidateUserOpQueryAsync(ValidateUserOpFunction validateUserOpFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ValidateUserOpQueryAsync(PackedUserOperation userOp, byte[] userOpHash, BlockParameter blockParameter = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        public Task<string> VersionQueryAsync(VersionFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(versionFunction, blockParameter);
        }

        
        public virtual Task<string> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AddCredentialFunction),
                typeof(CredentialDetailsFunction),
                typeof(GenerateCredentialIdFunction),
                typeof(GetCredentialCountFunction),
                typeof(GetCredentialIdsFunction),
                typeof(GetCredentialInfoFunction),
                typeof(HasCredentialFunction),
                typeof(HasCredentialByIdFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(RemoveCredentialFunction),
                typeof(SetThresholdFunction),
                typeof(ThresholdFunction),
                typeof(ValidateSignatureWithDataFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(CredentialAddedEventDTO),
                typeof(CredentialRemovedEventDTO),
                typeof(ModuleInitializedEventDTO),
                typeof(ModuleUninitializedEventDTO),
                typeof(ThresholdSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(CannotRemoveCredentialError),
                typeof(CredentialAlreadyExistsError),
                typeof(InvalidCredentialError),
                typeof(InvalidPublicKeyError),
                typeof(InvalidThresholdError),
                typeof(MaxCredentialsReachedError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(NotSortedError),
                typeof(NotUniqueError),
                typeof(ThresholdNotSetError)
            };
        }
    }
}
