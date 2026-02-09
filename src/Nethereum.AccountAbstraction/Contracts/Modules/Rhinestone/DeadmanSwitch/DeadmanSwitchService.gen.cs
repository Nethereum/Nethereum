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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.DeadmanSwitch.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.DeadmanSwitch
{
    public partial class DeadmanSwitchService: DeadmanSwitchServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, DeadmanSwitchDeployment deadmanSwitchDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<DeadmanSwitchDeployment>().SendRequestAndWaitForReceiptAsync(deadmanSwitchDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, DeadmanSwitchDeployment deadmanSwitchDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DeadmanSwitchDeployment>().SendRequestAsync(deadmanSwitchDeployment);
        }

        public static async Task<DeadmanSwitchService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, DeadmanSwitchDeployment deadmanSwitchDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, deadmanSwitchDeployment, cancellationTokenSource);
            return new DeadmanSwitchService(web3, receipt.ContractAddress);
        }

        public DeadmanSwitchService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class DeadmanSwitchServiceBase: ContractWeb3ServiceBase
    {

        public DeadmanSwitchServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> ClearTrustedForwarderRequestAsync(ClearTrustedForwarderFunction clearTrustedForwarderFunction)
        {
             return ContractHandler.SendRequestAsync(clearTrustedForwarderFunction);
        }

        public virtual Task<string> ClearTrustedForwarderRequestAsync()
        {
             return ContractHandler.SendRequestAsync<ClearTrustedForwarderFunction>();
        }

        public virtual Task<TransactionReceipt> ClearTrustedForwarderRequestAndWaitForReceiptAsync(ClearTrustedForwarderFunction clearTrustedForwarderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(clearTrustedForwarderFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> ClearTrustedForwarderRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<ClearTrustedForwarderFunction>(null, cancellationToken);
        }

        public virtual Task<ConfigOutputDTO> ConfigQueryAsync(ConfigFunction configFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ConfigFunction, ConfigOutputDTO>(configFunction, blockParameter);
        }

        public virtual Task<ConfigOutputDTO> ConfigQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var configFunction = new ConfigFunction();
                configFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ConfigFunction, ConfigOutputDTO>(configFunction, blockParameter);
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

        public Task<bool> IsTrustedForwarderQueryAsync(IsTrustedForwarderFunction isTrustedForwarderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsTrustedForwarderFunction, bool>(isTrustedForwarderFunction, blockParameter);
        }

        
        public virtual Task<bool> IsTrustedForwarderQueryAsync(string forwarder, string account, BlockParameter blockParameter = null)
        {
            var isTrustedForwarderFunction = new IsTrustedForwarderFunction();
                isTrustedForwarderFunction.Forwarder = forwarder;
                isTrustedForwarderFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsTrustedForwarderFunction, bool>(isTrustedForwarderFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureWithSenderQueryAsync(IsValidSignatureWithSenderFunction isValidSignatureWithSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string returnValue1, byte[] returnValue2, byte[] returnValue3, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.ReturnValue1 = returnValue1;
                isValidSignatureWithSenderFunction.ReturnValue2 = returnValue2;
                isValidSignatureWithSenderFunction.ReturnValue3 = returnValue3;
            
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

        public virtual Task<string> PostCheckRequestAsync(PostCheckFunction postCheckFunction)
        {
             return ContractHandler.SendRequestAsync(postCheckFunction);
        }

        public virtual Task<TransactionReceipt> PostCheckRequestAndWaitForReceiptAsync(PostCheckFunction postCheckFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postCheckFunction, cancellationToken);
        }

        public virtual Task<string> PostCheckRequestAsync(byte[] hookData)
        {
            var postCheckFunction = new PostCheckFunction();
                postCheckFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAsync(postCheckFunction);
        }

        public virtual Task<TransactionReceipt> PostCheckRequestAndWaitForReceiptAsync(byte[] hookData, CancellationTokenSource cancellationToken = null)
        {
            var postCheckFunction = new PostCheckFunction();
                postCheckFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postCheckFunction, cancellationToken);
        }

        public virtual Task<string> PreCheckRequestAsync(PreCheckFunction preCheckFunction)
        {
             return ContractHandler.SendRequestAsync(preCheckFunction);
        }

        public virtual Task<TransactionReceipt> PreCheckRequestAndWaitForReceiptAsync(PreCheckFunction preCheckFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preCheckFunction, cancellationToken);
        }

        public virtual Task<string> PreCheckRequestAsync(string msgSender, BigInteger msgValue, byte[] msgData)
        {
            var preCheckFunction = new PreCheckFunction();
                preCheckFunction.MsgSender = msgSender;
                preCheckFunction.MsgValue = msgValue;
                preCheckFunction.MsgData = msgData;
            
             return ContractHandler.SendRequestAsync(preCheckFunction);
        }

        public virtual Task<TransactionReceipt> PreCheckRequestAndWaitForReceiptAsync(string msgSender, BigInteger msgValue, byte[] msgData, CancellationTokenSource cancellationToken = null)
        {
            var preCheckFunction = new PreCheckFunction();
                preCheckFunction.MsgSender = msgSender;
                preCheckFunction.MsgValue = msgValue;
                preCheckFunction.MsgData = msgData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preCheckFunction, cancellationToken);
        }

        public virtual Task<string> SetNomineeRequestAsync(SetNomineeFunction setNomineeFunction)
        {
             return ContractHandler.SendRequestAsync(setNomineeFunction);
        }

        public virtual Task<TransactionReceipt> SetNomineeRequestAndWaitForReceiptAsync(SetNomineeFunction setNomineeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setNomineeFunction, cancellationToken);
        }

        public virtual Task<string> SetNomineeRequestAsync(string nominee)
        {
            var setNomineeFunction = new SetNomineeFunction();
                setNomineeFunction.Nominee = nominee;
            
             return ContractHandler.SendRequestAsync(setNomineeFunction);
        }

        public virtual Task<TransactionReceipt> SetNomineeRequestAndWaitForReceiptAsync(string nominee, CancellationTokenSource cancellationToken = null)
        {
            var setNomineeFunction = new SetNomineeFunction();
                setNomineeFunction.Nominee = nominee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setNomineeFunction, cancellationToken);
        }

        public virtual Task<string> SetTimeoutRequestAsync(SetTimeoutFunction setTimeoutFunction)
        {
             return ContractHandler.SendRequestAsync(setTimeoutFunction);
        }

        public virtual Task<TransactionReceipt> SetTimeoutRequestAndWaitForReceiptAsync(SetTimeoutFunction setTimeoutFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTimeoutFunction, cancellationToken);
        }

        public virtual Task<string> SetTimeoutRequestAsync(ulong timeout)
        {
            var setTimeoutFunction = new SetTimeoutFunction();
                setTimeoutFunction.Timeout = timeout;
            
             return ContractHandler.SendRequestAsync(setTimeoutFunction);
        }

        public virtual Task<TransactionReceipt> SetTimeoutRequestAndWaitForReceiptAsync(ulong timeout, CancellationTokenSource cancellationToken = null)
        {
            var setTimeoutFunction = new SetTimeoutFunction();
                setTimeoutFunction.Timeout = timeout;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTimeoutFunction, cancellationToken);
        }

        public virtual Task<string> SetTrustedForwarderRequestAsync(SetTrustedForwarderFunction setTrustedForwarderFunction)
        {
             return ContractHandler.SendRequestAsync(setTrustedForwarderFunction);
        }

        public virtual Task<TransactionReceipt> SetTrustedForwarderRequestAndWaitForReceiptAsync(SetTrustedForwarderFunction setTrustedForwarderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTrustedForwarderFunction, cancellationToken);
        }

        public virtual Task<string> SetTrustedForwarderRequestAsync(string forwarder)
        {
            var setTrustedForwarderFunction = new SetTrustedForwarderFunction();
                setTrustedForwarderFunction.Forwarder = forwarder;
            
             return ContractHandler.SendRequestAsync(setTrustedForwarderFunction);
        }

        public virtual Task<TransactionReceipt> SetTrustedForwarderRequestAndWaitForReceiptAsync(string forwarder, CancellationTokenSource cancellationToken = null)
        {
            var setTrustedForwarderFunction = new SetTrustedForwarderFunction();
                setTrustedForwarderFunction.Forwarder = forwarder;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTrustedForwarderFunction, cancellationToken);
        }

        public Task<string> TrustedForwarderQueryAsync(TrustedForwarderFunction trustedForwarderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TrustedForwarderFunction, string>(trustedForwarderFunction, blockParameter);
        }

        
        public virtual Task<string> TrustedForwarderQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var trustedForwarderFunction = new TrustedForwarderFunction();
                trustedForwarderFunction.Account = account;
            
            return ContractHandler.QueryAsync<TrustedForwarderFunction, string>(trustedForwarderFunction, blockParameter);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(ValidateUserOpFunction validateUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(ValidateUserOpFunction validateUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, CancellationTokenSource cancellationToken = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
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
                typeof(ClearTrustedForwarderFunction),
                typeof(ConfigFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsTrustedForwarderFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(PostCheckFunction),
                typeof(PreCheckFunction),
                typeof(SetNomineeFunction),
                typeof(SetTimeoutFunction),
                typeof(SetTrustedForwarderFunction),
                typeof(TrustedForwarderFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ModuleInitializedEventDTO),
                typeof(ModuleUninitializedEventDTO),
                typeof(NomineeSetEventDTO),
                typeof(TimeoutSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(UnsupportedOperationError)
            };
        }
    }
}
