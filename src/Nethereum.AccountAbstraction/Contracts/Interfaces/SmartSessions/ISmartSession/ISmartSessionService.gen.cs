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
using Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession
{
    public partial class ISmartSessionService: ISmartSessionServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ISmartSessionDeployment iSmartSessionDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ISmartSessionDeployment>().SendRequestAndWaitForReceiptAsync(iSmartSessionDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ISmartSessionDeployment iSmartSessionDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ISmartSessionDeployment>().SendRequestAsync(iSmartSessionDeployment);
        }

        public static async Task<ISmartSessionService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ISmartSessionDeployment iSmartSessionDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iSmartSessionDeployment, cancellationTokenSource);
            return new ISmartSessionService(web3, receipt.ContractAddress);
        }

        public ISmartSessionService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ISmartSessionServiceBase: ContractWeb3ServiceBase
    {

        public ISmartSessionServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> DisableActionIdRequestAsync(DisableActionIdFunction disableActionIdFunction)
        {
             return ContractHandler.SendRequestAsync(disableActionIdFunction);
        }

        public virtual Task<TransactionReceipt> DisableActionIdRequestAndWaitForReceiptAsync(DisableActionIdFunction disableActionIdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableActionIdFunction, cancellationToken);
        }

        public virtual Task<string> DisableActionIdRequestAsync(byte[] permissionId, byte[] actionId)
        {
            var disableActionIdFunction = new DisableActionIdFunction();
                disableActionIdFunction.PermissionId = permissionId;
                disableActionIdFunction.ActionId = actionId;
            
             return ContractHandler.SendRequestAsync(disableActionIdFunction);
        }

        public virtual Task<TransactionReceipt> DisableActionIdRequestAndWaitForReceiptAsync(byte[] permissionId, byte[] actionId, CancellationTokenSource cancellationToken = null)
        {
            var disableActionIdFunction = new DisableActionIdFunction();
                disableActionIdFunction.PermissionId = permissionId;
                disableActionIdFunction.ActionId = actionId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableActionIdFunction, cancellationToken);
        }

        public virtual Task<string> DisableActionPoliciesRequestAsync(DisableActionPoliciesFunction disableActionPoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(disableActionPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableActionPoliciesRequestAndWaitForReceiptAsync(DisableActionPoliciesFunction disableActionPoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableActionPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> DisableActionPoliciesRequestAsync(byte[] permissionId, byte[] actionId, List<string> policies)
        {
            var disableActionPoliciesFunction = new DisableActionPoliciesFunction();
                disableActionPoliciesFunction.PermissionId = permissionId;
                disableActionPoliciesFunction.ActionId = actionId;
                disableActionPoliciesFunction.Policies = policies;
            
             return ContractHandler.SendRequestAsync(disableActionPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableActionPoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, byte[] actionId, List<string> policies, CancellationTokenSource cancellationToken = null)
        {
            var disableActionPoliciesFunction = new DisableActionPoliciesFunction();
                disableActionPoliciesFunction.PermissionId = permissionId;
                disableActionPoliciesFunction.ActionId = actionId;
                disableActionPoliciesFunction.Policies = policies;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableActionPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> DisableERC1271PoliciesRequestAsync(DisableERC1271PoliciesFunction disableERC1271PoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(disableERC1271PoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableERC1271PoliciesRequestAndWaitForReceiptAsync(DisableERC1271PoliciesFunction disableERC1271PoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableERC1271PoliciesFunction, cancellationToken);
        }

        public virtual Task<string> DisableERC1271PoliciesRequestAsync(byte[] permissionId, List<string> policies, List<ERC7739Context> contexts)
        {
            var disableERC1271PoliciesFunction = new DisableERC1271PoliciesFunction();
                disableERC1271PoliciesFunction.PermissionId = permissionId;
                disableERC1271PoliciesFunction.Policies = policies;
                disableERC1271PoliciesFunction.Contexts = contexts;
            
             return ContractHandler.SendRequestAsync(disableERC1271PoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableERC1271PoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, List<string> policies, List<ERC7739Context> contexts, CancellationTokenSource cancellationToken = null)
        {
            var disableERC1271PoliciesFunction = new DisableERC1271PoliciesFunction();
                disableERC1271PoliciesFunction.PermissionId = permissionId;
                disableERC1271PoliciesFunction.Policies = policies;
                disableERC1271PoliciesFunction.Contexts = contexts;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableERC1271PoliciesFunction, cancellationToken);
        }

        public virtual Task<string> DisableUserOpPoliciesRequestAsync(DisableUserOpPoliciesFunction disableUserOpPoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(disableUserOpPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableUserOpPoliciesRequestAndWaitForReceiptAsync(DisableUserOpPoliciesFunction disableUserOpPoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableUserOpPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> DisableUserOpPoliciesRequestAsync(byte[] permissionId, List<string> policies)
        {
            var disableUserOpPoliciesFunction = new DisableUserOpPoliciesFunction();
                disableUserOpPoliciesFunction.PermissionId = permissionId;
                disableUserOpPoliciesFunction.Policies = policies;
            
             return ContractHandler.SendRequestAsync(disableUserOpPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> DisableUserOpPoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, List<string> policies, CancellationTokenSource cancellationToken = null)
        {
            var disableUserOpPoliciesFunction = new DisableUserOpPoliciesFunction();
                disableUserOpPoliciesFunction.PermissionId = permissionId;
                disableUserOpPoliciesFunction.Policies = policies;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(disableUserOpPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableActionPoliciesRequestAsync(EnableActionPoliciesFunction enableActionPoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(enableActionPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableActionPoliciesRequestAndWaitForReceiptAsync(EnableActionPoliciesFunction enableActionPoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableActionPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableActionPoliciesRequestAsync(byte[] permissionId, List<ActionData> actionPolicies)
        {
            var enableActionPoliciesFunction = new EnableActionPoliciesFunction();
                enableActionPoliciesFunction.PermissionId = permissionId;
                enableActionPoliciesFunction.ActionPolicies = actionPolicies;
            
             return ContractHandler.SendRequestAsync(enableActionPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableActionPoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, List<ActionData> actionPolicies, CancellationTokenSource cancellationToken = null)
        {
            var enableActionPoliciesFunction = new EnableActionPoliciesFunction();
                enableActionPoliciesFunction.PermissionId = permissionId;
                enableActionPoliciesFunction.ActionPolicies = actionPolicies;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableActionPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableERC1271PoliciesRequestAsync(EnableERC1271PoliciesFunction enableERC1271PoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(enableERC1271PoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableERC1271PoliciesRequestAndWaitForReceiptAsync(EnableERC1271PoliciesFunction enableERC1271PoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableERC1271PoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableERC1271PoliciesRequestAsync(byte[] permissionId, ERC7739Data erc1271Policies)
        {
            var enableERC1271PoliciesFunction = new EnableERC1271PoliciesFunction();
                enableERC1271PoliciesFunction.PermissionId = permissionId;
                enableERC1271PoliciesFunction.Erc1271Policies = erc1271Policies;
            
             return ContractHandler.SendRequestAsync(enableERC1271PoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableERC1271PoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, ERC7739Data erc1271Policies, CancellationTokenSource cancellationToken = null)
        {
            var enableERC1271PoliciesFunction = new EnableERC1271PoliciesFunction();
                enableERC1271PoliciesFunction.PermissionId = permissionId;
                enableERC1271PoliciesFunction.Erc1271Policies = erc1271Policies;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableERC1271PoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableSessionsRequestAsync(EnableSessionsFunction enableSessionsFunction)
        {
             return ContractHandler.SendRequestAsync(enableSessionsFunction);
        }

        public virtual Task<TransactionReceipt> EnableSessionsRequestAndWaitForReceiptAsync(EnableSessionsFunction enableSessionsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableSessionsFunction, cancellationToken);
        }

        public virtual Task<string> EnableSessionsRequestAsync(List<Session> sessions)
        {
            var enableSessionsFunction = new EnableSessionsFunction();
                enableSessionsFunction.Sessions = sessions;
            
             return ContractHandler.SendRequestAsync(enableSessionsFunction);
        }

        public virtual Task<TransactionReceipt> EnableSessionsRequestAndWaitForReceiptAsync(List<Session> sessions, CancellationTokenSource cancellationToken = null)
        {
            var enableSessionsFunction = new EnableSessionsFunction();
                enableSessionsFunction.Sessions = sessions;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableSessionsFunction, cancellationToken);
        }

        public virtual Task<string> EnableUserOpPoliciesRequestAsync(EnableUserOpPoliciesFunction enableUserOpPoliciesFunction)
        {
             return ContractHandler.SendRequestAsync(enableUserOpPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableUserOpPoliciesRequestAndWaitForReceiptAsync(EnableUserOpPoliciesFunction enableUserOpPoliciesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableUserOpPoliciesFunction, cancellationToken);
        }

        public virtual Task<string> EnableUserOpPoliciesRequestAsync(byte[] permissionId, List<PolicyData> userOpPolicies)
        {
            var enableUserOpPoliciesFunction = new EnableUserOpPoliciesFunction();
                enableUserOpPoliciesFunction.PermissionId = permissionId;
                enableUserOpPoliciesFunction.UserOpPolicies = userOpPolicies;
            
             return ContractHandler.SendRequestAsync(enableUserOpPoliciesFunction);
        }

        public virtual Task<TransactionReceipt> EnableUserOpPoliciesRequestAndWaitForReceiptAsync(byte[] permissionId, List<PolicyData> userOpPolicies, CancellationTokenSource cancellationToken = null)
        {
            var enableUserOpPoliciesFunction = new EnableUserOpPoliciesFunction();
                enableUserOpPoliciesFunction.PermissionId = permissionId;
                enableUserOpPoliciesFunction.UserOpPolicies = userOpPolicies;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableUserOpPoliciesFunction, cancellationToken);
        }

        public Task<BigInteger> GetNonceQueryAsync(GetNonceFunction getNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetNonceQueryAsync(byte[] permissionId, string account, BlockParameter blockParameter = null)
        {
            var getNonceFunction = new GetNonceFunction();
                getNonceFunction.PermissionId = permissionId;
                getNonceFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        public Task<List<byte[]>> GetPermissionIDsQueryAsync(GetPermissionIDsFunction getPermissionIDsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPermissionIDsFunction, List<byte[]>>(getPermissionIDsFunction, blockParameter);
        }

        
        public virtual Task<List<byte[]>> GetPermissionIDsQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getPermissionIDsFunction = new GetPermissionIDsFunction();
                getPermissionIDsFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetPermissionIDsFunction, List<byte[]>>(getPermissionIDsFunction, blockParameter);
        }

        public Task<byte[]> GetPermissionIdQueryAsync(GetPermissionIdFunction getPermissionIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPermissionIdFunction, byte[]>(getPermissionIdFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetPermissionIdQueryAsync(Session session, BlockParameter blockParameter = null)
        {
            var getPermissionIdFunction = new GetPermissionIdFunction();
                getPermissionIdFunction.Session = session;
            
            return ContractHandler.QueryAsync<GetPermissionIdFunction, byte[]>(getPermissionIdFunction, blockParameter);
        }

        public Task<byte[]> GetSessionDigestQueryAsync(GetSessionDigestFunction getSessionDigestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetSessionDigestFunction, byte[]>(getSessionDigestFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetSessionDigestQueryAsync(byte[] permissionId, string account, Session data, byte mode, BlockParameter blockParameter = null)
        {
            var getSessionDigestFunction = new GetSessionDigestFunction();
                getSessionDigestFunction.PermissionId = permissionId;
                getSessionDigestFunction.Account = account;
                getSessionDigestFunction.Data = data;
                getSessionDigestFunction.Mode = mode;
            
            return ContractHandler.QueryAsync<GetSessionDigestFunction, byte[]>(getSessionDigestFunction, blockParameter);
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

        public Task<bool> IsPermissionEnabledQueryAsync(IsPermissionEnabledFunction isPermissionEnabledFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsPermissionEnabledFunction, bool>(isPermissionEnabledFunction, blockParameter);
        }

        
        public virtual Task<bool> IsPermissionEnabledQueryAsync(byte[] permissionId, string account, BlockParameter blockParameter = null)
        {
            var isPermissionEnabledFunction = new IsPermissionEnabledFunction();
                isPermissionEnabledFunction.PermissionId = permissionId;
                isPermissionEnabledFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsPermissionEnabledFunction, bool>(isPermissionEnabledFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureWithSenderQueryAsync(IsValidSignatureWithSenderFunction isValidSignatureWithSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string sender, byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.Sender = sender;
                isValidSignatureWithSenderFunction.Hash = hash;
                isValidSignatureWithSenderFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
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

        public virtual Task<string> RemoveSessionRequestAsync(RemoveSessionFunction removeSessionFunction)
        {
             return ContractHandler.SendRequestAsync(removeSessionFunction);
        }

        public virtual Task<TransactionReceipt> RemoveSessionRequestAndWaitForReceiptAsync(RemoveSessionFunction removeSessionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeSessionFunction, cancellationToken);
        }

        public virtual Task<string> RemoveSessionRequestAsync(byte[] permissionId)
        {
            var removeSessionFunction = new RemoveSessionFunction();
                removeSessionFunction.PermissionId = permissionId;
            
             return ContractHandler.SendRequestAsync(removeSessionFunction);
        }

        public virtual Task<TransactionReceipt> RemoveSessionRequestAndWaitForReceiptAsync(byte[] permissionId, CancellationTokenSource cancellationToken = null)
        {
            var removeSessionFunction = new RemoveSessionFunction();
                removeSessionFunction.PermissionId = permissionId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeSessionFunction, cancellationToken);
        }

        public virtual Task<string> RevokeEnableSignatureRequestAsync(RevokeEnableSignatureFunction revokeEnableSignatureFunction)
        {
             return ContractHandler.SendRequestAsync(revokeEnableSignatureFunction);
        }

        public virtual Task<TransactionReceipt> RevokeEnableSignatureRequestAndWaitForReceiptAsync(RevokeEnableSignatureFunction revokeEnableSignatureFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeEnableSignatureFunction, cancellationToken);
        }

        public virtual Task<string> RevokeEnableSignatureRequestAsync(byte[] permissionId)
        {
            var revokeEnableSignatureFunction = new RevokeEnableSignatureFunction();
                revokeEnableSignatureFunction.PermissionId = permissionId;
            
             return ContractHandler.SendRequestAsync(revokeEnableSignatureFunction);
        }

        public virtual Task<TransactionReceipt> RevokeEnableSignatureRequestAndWaitForReceiptAsync(byte[] permissionId, CancellationTokenSource cancellationToken = null)
        {
            var revokeEnableSignatureFunction = new RevokeEnableSignatureFunction();
                revokeEnableSignatureFunction.PermissionId = permissionId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeEnableSignatureFunction, cancellationToken);
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DisableActionIdFunction),
                typeof(DisableActionPoliciesFunction),
                typeof(DisableERC1271PoliciesFunction),
                typeof(DisableUserOpPoliciesFunction),
                typeof(EnableActionPoliciesFunction),
                typeof(EnableERC1271PoliciesFunction),
                typeof(EnableSessionsFunction),
                typeof(EnableUserOpPoliciesFunction),
                typeof(GetNonceFunction),
                typeof(GetPermissionIDsFunction),
                typeof(GetPermissionIdFunction),
                typeof(GetSessionDigestFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsPermissionEnabledFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(RemoveSessionFunction),
                typeof(RevokeEnableSignatureFunction),
                typeof(ValidateUserOpFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ActionIdDisabledEventDTO),
                typeof(NonceIteratedEventDTO),
                typeof(PermissionIdPermit4337PaymasterEventDTO),
                typeof(PolicyDisabledEventDTO),
                typeof(PolicyEnabledEventDTO),
                typeof(SessionCreatedEventDTO),
                typeof(SessionRemovedEventDTO),
                typeof(SessionValidatorDisabledEventDTO),
                typeof(SessionValidatorEnabledEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AssociatedarrayOutofboundsError),
                typeof(ChainIdMismatchError),
                typeof(ForbiddenValidationDataError),
                typeof(HashIndexOutOfBoundsError),
                typeof(HashMismatchError),
                typeof(InvalidActionIdError),
                typeof(InvalidCallTargetError),
                typeof(InvalidDataError),
                typeof(InvalidEnableSignatureError),
                typeof(InvalidISessionValidatorError),
                typeof(InvalidModeError),
                typeof(InvalidPermissionIdError),
                typeof(InvalidSelfCallError),
                typeof(InvalidSessionError),
                typeof(InvalidSessionKeySignatureError),
                typeof(InvalidTargetError),
                typeof(InvalidUserOpSenderError),
                typeof(NoExecutionsInBatchError),
                typeof(NoPoliciesSetError),
                typeof(PartlyEnabledActionsError),
                typeof(PartlyEnabledPoliciesError),
                typeof(PaymasterValidationNotEnabledError),
                typeof(PolicyViolationError),
                typeof(SignerNotFoundError),
                typeof(SmartSessionModuleAlreadyInstalledError),
                typeof(UnsupportedExecutionTypeError),
                typeof(UnsupportedPolicyError),
                typeof(UnsupportedSmartSessionModeError)
            };
        }
    }
}
