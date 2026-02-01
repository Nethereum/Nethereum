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
using Nethereum.AccountAbstraction.Contracts.Modules.SessionKeyModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SessionKeyModule
{
    public partial class SessionKeyModuleService: SessionKeyModuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SessionKeyModuleDeployment sessionKeyModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SessionKeyModuleDeployment>().SendRequestAndWaitForReceiptAsync(sessionKeyModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SessionKeyModuleDeployment sessionKeyModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SessionKeyModuleDeployment>().SendRequestAsync(sessionKeyModuleDeployment);
        }

        public static async Task<SessionKeyModuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SessionKeyModuleDeployment sessionKeyModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, sessionKeyModuleDeployment, cancellationTokenSource);
            return new SessionKeyModuleService(web3, receipt.ContractAddress);
        }

        public SessionKeyModuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SessionKeyModuleServiceBase: ContractWeb3ServiceBase
    {

        public SessionKeyModuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddSessionKeyRequestAsync(AddSessionKeyFunction addSessionKeyFunction)
        {
             return ContractHandler.SendRequestAsync(addSessionKeyFunction);
        }

        public virtual Task<TransactionReceipt> AddSessionKeyRequestAndWaitForReceiptAsync(AddSessionKeyFunction addSessionKeyFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addSessionKeyFunction, cancellationToken);
        }

        public virtual Task<string> AddSessionKeyRequestAsync(string account, string key, ulong validUntil, ulong validAfter, BigInteger maxValuePerOp, BigInteger dailyValueLimit, uint dailyOpLimit, PermissionFlags permissions, List<string> allowedTargets, List<byte[]> allowedSelectors)
        {
            var addSessionKeyFunction = new AddSessionKeyFunction();
                addSessionKeyFunction.Account = account;
                addSessionKeyFunction.Key = key;
                addSessionKeyFunction.ValidUntil = validUntil;
                addSessionKeyFunction.ValidAfter = validAfter;
                addSessionKeyFunction.MaxValuePerOp = maxValuePerOp;
                addSessionKeyFunction.DailyValueLimit = dailyValueLimit;
                addSessionKeyFunction.DailyOpLimit = dailyOpLimit;
                addSessionKeyFunction.Permissions = permissions;
                addSessionKeyFunction.AllowedTargets = allowedTargets;
                addSessionKeyFunction.AllowedSelectors = allowedSelectors;
            
             return ContractHandler.SendRequestAsync(addSessionKeyFunction);
        }

        public virtual Task<TransactionReceipt> AddSessionKeyRequestAndWaitForReceiptAsync(string account, string key, ulong validUntil, ulong validAfter, BigInteger maxValuePerOp, BigInteger dailyValueLimit, uint dailyOpLimit, PermissionFlags permissions, List<string> allowedTargets, List<byte[]> allowedSelectors, CancellationTokenSource cancellationToken = null)
        {
            var addSessionKeyFunction = new AddSessionKeyFunction();
                addSessionKeyFunction.Account = account;
                addSessionKeyFunction.Key = key;
                addSessionKeyFunction.ValidUntil = validUntil;
                addSessionKeyFunction.ValidAfter = validAfter;
                addSessionKeyFunction.MaxValuePerOp = maxValuePerOp;
                addSessionKeyFunction.DailyValueLimit = dailyValueLimit;
                addSessionKeyFunction.DailyOpLimit = dailyOpLimit;
                addSessionKeyFunction.Permissions = permissions;
                addSessionKeyFunction.AllowedTargets = allowedTargets;
                addSessionKeyFunction.AllowedSelectors = allowedSelectors;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addSessionKeyFunction, cancellationToken);
        }

        public Task<BigInteger> GetSessionKeyCountQueryAsync(GetSessionKeyCountFunction getSessionKeyCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetSessionKeyCountFunction, BigInteger>(getSessionKeyCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetSessionKeyCountQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getSessionKeyCountFunction = new GetSessionKeyCountFunction();
                getSessionKeyCountFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetSessionKeyCountFunction, BigInteger>(getSessionKeyCountFunction, blockParameter);
        }

        public virtual Task<GetSessionKeyInfoOutputDTO> GetSessionKeyInfoQueryAsync(GetSessionKeyInfoFunction getSessionKeyInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetSessionKeyInfoFunction, GetSessionKeyInfoOutputDTO>(getSessionKeyInfoFunction, blockParameter);
        }

        public virtual Task<GetSessionKeyInfoOutputDTO> GetSessionKeyInfoQueryAsync(string account, string key, BlockParameter blockParameter = null)
        {
            var getSessionKeyInfoFunction = new GetSessionKeyInfoFunction();
                getSessionKeyInfoFunction.Account = account;
                getSessionKeyInfoFunction.Key = key;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetSessionKeyInfoFunction, GetSessionKeyInfoOutputDTO>(getSessionKeyInfoFunction, blockParameter);
        }

        public Task<List<string>> GetSessionKeysQueryAsync(GetSessionKeysFunction getSessionKeysFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetSessionKeysFunction, List<string>>(getSessionKeysFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetSessionKeysQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getSessionKeysFunction = new GetSessionKeysFunction();
                getSessionKeysFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetSessionKeysFunction, List<string>>(getSessionKeysFunction, blockParameter);
        }

        public Task<bool> IsValidSignatureQueryAsync(IsValidSignatureFunction isValidSignatureFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureFunction, bool>(isValidSignatureFunction, blockParameter);
        }

        
        public virtual Task<bool> IsValidSignatureQueryAsync(byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureFunction = new IsValidSignatureFunction();
                isValidSignatureFunction.Hash = hash;
                isValidSignatureFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<IsValidSignatureFunction, bool>(isValidSignatureFunction, blockParameter);
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

        public virtual Task<string> RevokeAllSessionKeysRequestAsync(RevokeAllSessionKeysFunction revokeAllSessionKeysFunction)
        {
             return ContractHandler.SendRequestAsync(revokeAllSessionKeysFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAllSessionKeysRequestAndWaitForReceiptAsync(RevokeAllSessionKeysFunction revokeAllSessionKeysFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAllSessionKeysFunction, cancellationToken);
        }

        public virtual Task<string> RevokeAllSessionKeysRequestAsync(string account)
        {
            var revokeAllSessionKeysFunction = new RevokeAllSessionKeysFunction();
                revokeAllSessionKeysFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(revokeAllSessionKeysFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAllSessionKeysRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var revokeAllSessionKeysFunction = new RevokeAllSessionKeysFunction();
                revokeAllSessionKeysFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAllSessionKeysFunction, cancellationToken);
        }

        public virtual Task<string> RevokeSessionKeyRequestAsync(RevokeSessionKeyFunction revokeSessionKeyFunction)
        {
             return ContractHandler.SendRequestAsync(revokeSessionKeyFunction);
        }

        public virtual Task<TransactionReceipt> RevokeSessionKeyRequestAndWaitForReceiptAsync(RevokeSessionKeyFunction revokeSessionKeyFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeSessionKeyFunction, cancellationToken);
        }

        public virtual Task<string> RevokeSessionKeyRequestAsync(string account, string key)
        {
            var revokeSessionKeyFunction = new RevokeSessionKeyFunction();
                revokeSessionKeyFunction.Account = account;
                revokeSessionKeyFunction.Key = key;
            
             return ContractHandler.SendRequestAsync(revokeSessionKeyFunction);
        }

        public virtual Task<TransactionReceipt> RevokeSessionKeyRequestAndWaitForReceiptAsync(string account, string key, CancellationTokenSource cancellationToken = null)
        {
            var revokeSessionKeyFunction = new RevokeSessionKeyFunction();
                revokeSessionKeyFunction.Account = account;
                revokeSessionKeyFunction.Key = key;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeSessionKeyFunction, cancellationToken);
        }

        public virtual Task<SessionKeyUsageOutputDTO> SessionKeyUsageQueryAsync(SessionKeyUsageFunction sessionKeyUsageFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<SessionKeyUsageFunction, SessionKeyUsageOutputDTO>(sessionKeyUsageFunction, blockParameter);
        }

        public virtual Task<SessionKeyUsageOutputDTO> SessionKeyUsageQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var sessionKeyUsageFunction = new SessionKeyUsageFunction();
                sessionKeyUsageFunction.ReturnValue1 = returnValue1;
                sessionKeyUsageFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<SessionKeyUsageFunction, SessionKeyUsageOutputDTO>(sessionKeyUsageFunction, blockParameter);
        }

        public virtual Task<SessionKeysOutputDTO> SessionKeysQueryAsync(SessionKeysFunction sessionKeysFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<SessionKeysFunction, SessionKeysOutputDTO>(sessionKeysFunction, blockParameter);
        }

        public virtual Task<SessionKeysOutputDTO> SessionKeysQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var sessionKeysFunction = new SessionKeysFunction();
                sessionKeysFunction.ReturnValue1 = returnValue1;
                sessionKeysFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<SessionKeysFunction, SessionKeysOutputDTO>(sessionKeysFunction, blockParameter);
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
                typeof(AddSessionKeyFunction),
                typeof(GetSessionKeyCountFunction),
                typeof(GetSessionKeyInfoFunction),
                typeof(GetSessionKeysFunction),
                typeof(IsValidSignatureFunction),
                typeof(ModuleIdFunction),
                typeof(PostExecuteFunction),
                typeof(PreExecuteFunction),
                typeof(RevokeAllSessionKeysFunction),
                typeof(RevokeSessionKeyFunction),
                typeof(SessionKeyUsageFunction),
                typeof(SessionKeysFunction),
                typeof(SupportsInterfaceFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AllSessionKeysRevokedEventDTO),
                typeof(SessionKeyAddedEventDTO),
                typeof(SessionKeyRevokedEventDTO),
                typeof(SessionKeyUsedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(DailyOpLimitExceededError),
                typeof(DailyValueLimitExceededError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(InvalidSessionKeyError),
                typeof(OnlyAccountError),
                typeof(PermissionDeniedError),
                typeof(SelectorNotAllowedError),
                typeof(SessionKeyExpiredError),
                typeof(SessionKeyNotYetValidError),
                typeof(TargetNotAllowedError),
                typeof(ValueExceedsLimitError)
            };
        }
    }
}
