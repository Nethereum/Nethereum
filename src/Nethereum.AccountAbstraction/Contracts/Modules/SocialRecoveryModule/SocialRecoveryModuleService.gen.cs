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
using Nethereum.AccountAbstraction.Contracts.Modules.SocialRecoveryModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SocialRecoveryModule
{
    public partial class SocialRecoveryModuleService: SocialRecoveryModuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryModuleDeployment socialRecoveryModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SocialRecoveryModuleDeployment>().SendRequestAndWaitForReceiptAsync(socialRecoveryModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryModuleDeployment socialRecoveryModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SocialRecoveryModuleDeployment>().SendRequestAsync(socialRecoveryModuleDeployment);
        }

        public static async Task<SocialRecoveryModuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryModuleDeployment socialRecoveryModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, socialRecoveryModuleDeployment, cancellationTokenSource);
            return new SocialRecoveryModuleService(web3, receipt.ContractAddress);
        }

        public SocialRecoveryModuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SocialRecoveryModuleServiceBase: ContractWeb3ServiceBase
    {

        public SocialRecoveryModuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddGuardianRequestAsync(AddGuardianFunction addGuardianFunction)
        {
             return ContractHandler.SendRequestAsync(addGuardianFunction);
        }

        public virtual Task<TransactionReceipt> AddGuardianRequestAndWaitForReceiptAsync(AddGuardianFunction addGuardianFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addGuardianFunction, cancellationToken);
        }

        public virtual Task<string> AddGuardianRequestAsync(string account, string guardian)
        {
            var addGuardianFunction = new AddGuardianFunction();
                addGuardianFunction.Account = account;
                addGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAsync(addGuardianFunction);
        }

        public virtual Task<TransactionReceipt> AddGuardianRequestAndWaitForReceiptAsync(string account, string guardian, CancellationTokenSource cancellationToken = null)
        {
            var addGuardianFunction = new AddGuardianFunction();
                addGuardianFunction.Account = account;
                addGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addGuardianFunction, cancellationToken);
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

        public Task<string> GetAccountFromRecoveryIdQueryAsync(GetAccountFromRecoveryIdFunction getAccountFromRecoveryIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAccountFromRecoveryIdFunction, string>(getAccountFromRecoveryIdFunction, blockParameter);
        }

        
        public virtual Task<string> GetAccountFromRecoveryIdQueryAsync(byte[] recoveryId, BlockParameter blockParameter = null)
        {
            var getAccountFromRecoveryIdFunction = new GetAccountFromRecoveryIdFunction();
                getAccountFromRecoveryIdFunction.RecoveryId = recoveryId;
            
            return ContractHandler.QueryAsync<GetAccountFromRecoveryIdFunction, string>(getAccountFromRecoveryIdFunction, blockParameter);
        }

        public Task<byte[]> GetCurrentRecoveryIdQueryAsync(GetCurrentRecoveryIdFunction getCurrentRecoveryIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetCurrentRecoveryIdFunction, byte[]>(getCurrentRecoveryIdFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetCurrentRecoveryIdQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getCurrentRecoveryIdFunction = new GetCurrentRecoveryIdFunction();
                getCurrentRecoveryIdFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetCurrentRecoveryIdFunction, byte[]>(getCurrentRecoveryIdFunction, blockParameter);
        }

        public Task<BigInteger> GetGuardianCountQueryAsync(GetGuardianCountFunction getGuardianCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetGuardianCountFunction, BigInteger>(getGuardianCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetGuardianCountQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getGuardianCountFunction = new GetGuardianCountFunction();
                getGuardianCountFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetGuardianCountFunction, BigInteger>(getGuardianCountFunction, blockParameter);
        }

        public Task<List<string>> GetGuardiansQueryAsync(GetGuardiansFunction getGuardiansFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetGuardiansFunction, List<string>>(getGuardiansFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetGuardiansQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getGuardiansFunction = new GetGuardiansFunction();
                getGuardiansFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetGuardiansFunction, List<string>>(getGuardiansFunction, blockParameter);
        }

        public Task<BigInteger> GetRecoveryDelayQueryAsync(GetRecoveryDelayFunction getRecoveryDelayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRecoveryDelayFunction, BigInteger>(getRecoveryDelayFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRecoveryDelayQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRecoveryDelayFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> GetRecoveryDelayForAccountQueryAsync(GetRecoveryDelayForAccountFunction getRecoveryDelayForAccountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRecoveryDelayForAccountFunction, BigInteger>(getRecoveryDelayForAccountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRecoveryDelayForAccountQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getRecoveryDelayForAccountFunction = new GetRecoveryDelayForAccountFunction();
                getRecoveryDelayForAccountFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetRecoveryDelayForAccountFunction, BigInteger>(getRecoveryDelayForAccountFunction, blockParameter);
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

        public Task<BigInteger> GetThresholdQueryAsync(GetThresholdFunction getThresholdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetThresholdFunction, BigInteger>(getThresholdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetThresholdQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getThresholdFunction = new GetThresholdFunction();
                getThresholdFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetThresholdFunction, BigInteger>(getThresholdFunction, blockParameter);
        }

        public Task<bool> HasApprovedQueryAsync(HasApprovedFunction hasApprovedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HasApprovedFunction, bool>(hasApprovedFunction, blockParameter);
        }

        
        public virtual Task<bool> HasApprovedQueryAsync(byte[] recoveryId, string guardian, BlockParameter blockParameter = null)
        {
            var hasApprovedFunction = new HasApprovedFunction();
                hasApprovedFunction.RecoveryId = recoveryId;
                hasApprovedFunction.Guardian = guardian;
            
            return ContractHandler.QueryAsync<HasApprovedFunction, bool>(hasApprovedFunction, blockParameter);
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

        public Task<byte[]> ModuleIdQueryAsync(ModuleIdFunction moduleIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleIdFunction, byte[]>(moduleIdFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ModuleIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleIdFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<string> PostExecuteRequestAsync(PostExecuteFunction postExecuteFunction)
        {
             return ContractHandler.SendRequestAsync(postExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PostExecuteRequestAndWaitForReceiptAsync(PostExecuteFunction postExecuteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PostExecuteRequestAsync(string returnValue1, BigInteger returnValue2, byte[] returnValue3)
        {
            var postExecuteFunction = new PostExecuteFunction();
                postExecuteFunction.ReturnValue1 = returnValue1;
                postExecuteFunction.ReturnValue2 = returnValue2;
                postExecuteFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAsync(postExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PostExecuteRequestAndWaitForReceiptAsync(string returnValue1, BigInteger returnValue2, byte[] returnValue3, CancellationTokenSource cancellationToken = null)
        {
            var postExecuteFunction = new PostExecuteFunction();
                postExecuteFunction.ReturnValue1 = returnValue1;
                postExecuteFunction.ReturnValue2 = returnValue2;
                postExecuteFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PreExecuteRequestAsync(PreExecuteFunction preExecuteFunction)
        {
             return ContractHandler.SendRequestAsync(preExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PreExecuteRequestAndWaitForReceiptAsync(PreExecuteFunction preExecuteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PreExecuteRequestAsync(string returnValue1, BigInteger returnValue2, byte[] returnValue3)
        {
            var preExecuteFunction = new PreExecuteFunction();
                preExecuteFunction.ReturnValue1 = returnValue1;
                preExecuteFunction.ReturnValue2 = returnValue2;
                preExecuteFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAsync(preExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PreExecuteRequestAndWaitForReceiptAsync(string returnValue1, BigInteger returnValue2, byte[] returnValue3, CancellationTokenSource cancellationToken = null)
        {
            var preExecuteFunction = new PreExecuteFunction();
                preExecuteFunction.ReturnValue1 = returnValue1;
                preExecuteFunction.ReturnValue2 = returnValue2;
                preExecuteFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preExecuteFunction, cancellationToken);
        }

        public virtual Task<string> RemoveGuardianRequestAsync(RemoveGuardianFunction removeGuardianFunction)
        {
             return ContractHandler.SendRequestAsync(removeGuardianFunction);
        }

        public virtual Task<TransactionReceipt> RemoveGuardianRequestAndWaitForReceiptAsync(RemoveGuardianFunction removeGuardianFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeGuardianFunction, cancellationToken);
        }

        public virtual Task<string> RemoveGuardianRequestAsync(string account, string guardian)
        {
            var removeGuardianFunction = new RemoveGuardianFunction();
                removeGuardianFunction.Account = account;
                removeGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAsync(removeGuardianFunction);
        }

        public virtual Task<TransactionReceipt> RemoveGuardianRequestAndWaitForReceiptAsync(string account, string guardian, CancellationTokenSource cancellationToken = null)
        {
            var removeGuardianFunction = new RemoveGuardianFunction();
                removeGuardianFunction.Account = account;
                removeGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeGuardianFunction, cancellationToken);
        }

        public virtual Task<string> SetRecoveryDelayRequestAsync(SetRecoveryDelayFunction setRecoveryDelayFunction)
        {
             return ContractHandler.SendRequestAsync(setRecoveryDelayFunction);
        }

        public virtual Task<TransactionReceipt> SetRecoveryDelayRequestAndWaitForReceiptAsync(SetRecoveryDelayFunction setRecoveryDelayFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecoveryDelayFunction, cancellationToken);
        }

        public virtual Task<string> SetRecoveryDelayRequestAsync(string account, BigInteger delay)
        {
            var setRecoveryDelayFunction = new SetRecoveryDelayFunction();
                setRecoveryDelayFunction.Account = account;
                setRecoveryDelayFunction.Delay = delay;
            
             return ContractHandler.SendRequestAsync(setRecoveryDelayFunction);
        }

        public virtual Task<TransactionReceipt> SetRecoveryDelayRequestAndWaitForReceiptAsync(string account, BigInteger delay, CancellationTokenSource cancellationToken = null)
        {
            var setRecoveryDelayFunction = new SetRecoveryDelayFunction();
                setRecoveryDelayFunction.Account = account;
                setRecoveryDelayFunction.Delay = delay;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecoveryDelayFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(SetThresholdFunction setThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(SetThresholdFunction setThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(string account, BigInteger threshold)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Account = account;
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(string account, BigInteger threshold, CancellationTokenSource cancellationToken = null)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Account = account;
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<BigInteger> ValidateUserOpQueryAsync(ValidateUserOpFunction validateUserOpFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ValidateUserOpQueryAsync(PackedUserOperation returnValue1, byte[] returnValue2, BlockParameter blockParameter = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.ReturnValue1 = returnValue1;
                validateUserOpFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        public Task<BigInteger> VersionQueryAsync(VersionFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, BigInteger>(versionFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, BigInteger>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AddGuardianFunction),
                typeof(ApproveRecoveryFunction),
                typeof(CancelRecoveryFunction),
                typeof(ExecuteRecoveryFunction),
                typeof(GetAccountFromRecoveryIdFunction),
                typeof(GetCurrentRecoveryIdFunction),
                typeof(GetGuardianCountFunction),
                typeof(GetGuardiansFunction),
                typeof(GetRecoveryDelayFunction),
                typeof(GetRecoveryDelayForAccountFunction),
                typeof(GetRecoveryRequestFunction),
                typeof(GetRequiredApprovalsFunction),
                typeof(GetThresholdFunction),
                typeof(HasApprovedFunction),
                typeof(InitiateRecoveryFunction),
                typeof(IsApproverFunction),
                typeof(ModuleIdFunction),
                typeof(PostExecuteFunction),
                typeof(PreExecuteFunction),
                typeof(RemoveGuardianFunction),
                typeof(SetRecoveryDelayFunction),
                typeof(SetThresholdFunction),
                typeof(SupportsInterfaceFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(GuardianAddedEventDTO),
                typeof(GuardianRemovedEventDTO),
                typeof(RecoveryApprovedEventDTO),
                typeof(RecoveryCancelledEventDTO),
                typeof(RecoveryDelayChangedEventDTO),
                typeof(RecoveryExecutedEventDTO),
                typeof(RecoveryInitiatedEventDTO),
                typeof(ThresholdChangedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AlreadyApprovedError),
                typeof(GuardianAlreadyExistsError),
                typeof(GuardianNotExistsError),
                typeof(InvalidGuardianError),
                typeof(InvalidNewOwnerError),
                typeof(InvalidThresholdError),
                typeof(NoRecoveryPendingError),
                typeof(NotEnoughGuardiansError),
                typeof(OnlyAccountError),
                typeof(OnlyGuardianError),
                typeof(RecoveryAlreadyPendingError),
                typeof(RecoveryNotReadyError),
                typeof(ThresholdNotMetError)
            };
        }
    }
}
