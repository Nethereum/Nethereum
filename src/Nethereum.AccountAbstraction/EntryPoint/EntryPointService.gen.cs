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
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.EntryPoint
{
    public partial class EntryPointService: EntryPointServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, EntryPointDeployment entryPointDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EntryPointDeployment>().SendRequestAndWaitForReceiptAsync(entryPointDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, EntryPointDeployment entryPointDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EntryPointDeployment>().SendRequestAsync(entryPointDeployment);
        }

        public static async Task<EntryPointService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, EntryPointDeployment entryPointDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, entryPointDeployment, cancellationTokenSource);
            return new EntryPointService(web3, receipt.ContractAddress);
        }

        public EntryPointService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class EntryPointServiceBase: ContractWeb3ServiceBase
    {

        public EntryPointServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddStakeRequestAsync(AddStakeFunction addStakeFunction)
        {
             return ContractHandler.SendRequestAsync(addStakeFunction);
        }

        public virtual Task<TransactionReceipt> AddStakeRequestAndWaitForReceiptAsync(AddStakeFunction addStakeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addStakeFunction, cancellationToken);
        }

        public virtual Task<string> AddStakeRequestAsync(uint unstakeDelaySec)
        {
            var addStakeFunction = new AddStakeFunction();
                addStakeFunction.UnstakeDelaySec = unstakeDelaySec;
            
             return ContractHandler.SendRequestAsync(addStakeFunction);
        }

        public virtual Task<TransactionReceipt> AddStakeRequestAndWaitForReceiptAsync(uint unstakeDelaySec, CancellationTokenSource cancellationToken = null)
        {
            var addStakeFunction = new AddStakeFunction();
                addStakeFunction.UnstakeDelaySec = unstakeDelaySec;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addStakeFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> BalanceOfQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Account = account;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public virtual Task<string> DelegateAndRevertRequestAsync(DelegateAndRevertFunction delegateAndRevertFunction)
        {
             return ContractHandler.SendRequestAsync(delegateAndRevertFunction);
        }

        public virtual Task<TransactionReceipt> DelegateAndRevertRequestAndWaitForReceiptAsync(DelegateAndRevertFunction delegateAndRevertFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(delegateAndRevertFunction, cancellationToken);
        }

        public virtual Task<string> DelegateAndRevertRequestAsync(string target, byte[] data)
        {
            var delegateAndRevertFunction = new DelegateAndRevertFunction();
                delegateAndRevertFunction.Target = target;
                delegateAndRevertFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(delegateAndRevertFunction);
        }

        public virtual Task<TransactionReceipt> DelegateAndRevertRequestAndWaitForReceiptAsync(string target, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var delegateAndRevertFunction = new DelegateAndRevertFunction();
                delegateAndRevertFunction.Target = target;
                delegateAndRevertFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(delegateAndRevertFunction, cancellationToken);
        }

        public virtual Task<string> DepositToRequestAsync(DepositToFunction depositToFunction)
        {
             return ContractHandler.SendRequestAsync(depositToFunction);
        }

        public virtual Task<TransactionReceipt> DepositToRequestAndWaitForReceiptAsync(DepositToFunction depositToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositToFunction, cancellationToken);
        }

        public virtual Task<string> DepositToRequestAsync(string account)
        {
            var depositToFunction = new DepositToFunction();
                depositToFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(depositToFunction);
        }

        public virtual Task<TransactionReceipt> DepositToRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var depositToFunction = new DepositToFunction();
                depositToFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositToFunction, cancellationToken);
        }

        public virtual Task<Eip712DomainOutputDTO> Eip712DomainQueryAsync(Eip712DomainFunction eip712DomainFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Eip712DomainFunction, Eip712DomainOutputDTO>(eip712DomainFunction, blockParameter);
        }

        public virtual Task<Eip712DomainOutputDTO> Eip712DomainQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Eip712DomainFunction, Eip712DomainOutputDTO>(null, blockParameter);
        }

        public virtual Task<GetDepositInfoOutputDTO> GetDepositInfoQueryAsync(GetDepositInfoFunction getDepositInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetDepositInfoFunction, GetDepositInfoOutputDTO>(getDepositInfoFunction, blockParameter);
        }

        public virtual Task<GetDepositInfoOutputDTO> GetDepositInfoQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getDepositInfoFunction = new GetDepositInfoFunction();
                getDepositInfoFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetDepositInfoFunction, GetDepositInfoOutputDTO>(getDepositInfoFunction, blockParameter);
        }

        public Task<byte[]> GetDomainSeparatorV4QueryAsync(GetDomainSeparatorV4Function getDomainSeparatorV4Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDomainSeparatorV4Function, byte[]>(getDomainSeparatorV4Function, blockParameter);
        }

        
        public virtual Task<byte[]> GetDomainSeparatorV4QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDomainSeparatorV4Function, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> GetNonceQueryAsync(GetNonceFunction getNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetNonceQueryAsync(string sender, BigInteger key, BlockParameter blockParameter = null)
        {
            var getNonceFunction = new GetNonceFunction();
                getNonceFunction.Sender = sender;
                getNonceFunction.Key = key;
            
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        public Task<byte[]> GetPackedUserOpTypeHashQueryAsync(GetPackedUserOpTypeHashFunction getPackedUserOpTypeHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPackedUserOpTypeHashFunction, byte[]>(getPackedUserOpTypeHashFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetPackedUserOpTypeHashQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPackedUserOpTypeHashFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<string> GetSenderAddressRequestAsync(GetSenderAddressFunction getSenderAddressFunction)
        {
             return ContractHandler.SendRequestAsync(getSenderAddressFunction);
        }

        public virtual Task<TransactionReceipt> GetSenderAddressRequestAndWaitForReceiptAsync(GetSenderAddressFunction getSenderAddressFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(getSenderAddressFunction, cancellationToken);
        }

        public virtual Task<string> GetSenderAddressRequestAsync(byte[] initCode)
        {
            var getSenderAddressFunction = new GetSenderAddressFunction();
                getSenderAddressFunction.InitCode = initCode;
            
             return ContractHandler.SendRequestAsync(getSenderAddressFunction);
        }

        public virtual Task<TransactionReceipt> GetSenderAddressRequestAndWaitForReceiptAsync(byte[] initCode, CancellationTokenSource cancellationToken = null)
        {
            var getSenderAddressFunction = new GetSenderAddressFunction();
                getSenderAddressFunction.InitCode = initCode;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(getSenderAddressFunction, cancellationToken);
        }

        public Task<byte[]> GetUserOpHashQueryAsync(GetUserOpHashFunction getUserOpHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetUserOpHashFunction, byte[]>(getUserOpHashFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetUserOpHashQueryAsync(PackedUserOperation userOp, BlockParameter blockParameter = null)
        {
            var getUserOpHashFunction = new GetUserOpHashFunction();
                getUserOpHashFunction.UserOp = userOp;
            
            return ContractHandler.QueryAsync<GetUserOpHashFunction, byte[]>(getUserOpHashFunction, blockParameter);
        }

        public virtual Task<string> HandleAggregatedOpsRequestAsync(HandleAggregatedOpsFunction handleAggregatedOpsFunction)
        {
             return ContractHandler.SendRequestAsync(handleAggregatedOpsFunction);
        }

        public virtual Task<TransactionReceipt> HandleAggregatedOpsRequestAndWaitForReceiptAsync(HandleAggregatedOpsFunction handleAggregatedOpsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(handleAggregatedOpsFunction, cancellationToken);
        }

        public virtual Task<string> HandleAggregatedOpsRequestAsync(List<UserOpsPerAggregator> opsPerAggregator, string beneficiary)
        {
            var handleAggregatedOpsFunction = new HandleAggregatedOpsFunction();
                handleAggregatedOpsFunction.OpsPerAggregator = opsPerAggregator;
                handleAggregatedOpsFunction.Beneficiary = beneficiary;
            
             return ContractHandler.SendRequestAsync(handleAggregatedOpsFunction);
        }

        public virtual Task<TransactionReceipt> HandleAggregatedOpsRequestAndWaitForReceiptAsync(List<UserOpsPerAggregator> opsPerAggregator, string beneficiary, CancellationTokenSource cancellationToken = null)
        {
            var handleAggregatedOpsFunction = new HandleAggregatedOpsFunction();
                handleAggregatedOpsFunction.OpsPerAggregator = opsPerAggregator;
                handleAggregatedOpsFunction.Beneficiary = beneficiary;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(handleAggregatedOpsFunction, cancellationToken);
        }

        public virtual Task<string> HandleOpsRequestAsync(HandleOpsFunction handleOpsFunction)
        {
             return ContractHandler.SendRequestAsync(handleOpsFunction);
        }

        public virtual Task<TransactionReceipt> HandleOpsRequestAndWaitForReceiptAsync(HandleOpsFunction handleOpsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(handleOpsFunction, cancellationToken);
        }

        public virtual Task<string> HandleOpsRequestAsync(List<PackedUserOperation> ops, string beneficiary)
        {
            var handleOpsFunction = new HandleOpsFunction();
                handleOpsFunction.Ops = ops;
                handleOpsFunction.Beneficiary = beneficiary;
            
             return ContractHandler.SendRequestAsync(handleOpsFunction);
        }

        public virtual Task<TransactionReceipt> HandleOpsRequestAndWaitForReceiptAsync(List<PackedUserOperation> ops, string beneficiary, CancellationTokenSource cancellationToken = null)
        {
            var handleOpsFunction = new HandleOpsFunction();
                handleOpsFunction.Ops = ops;
                handleOpsFunction.Beneficiary = beneficiary;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(handleOpsFunction, cancellationToken);
        }

        public virtual Task<string> IncrementNonceRequestAsync(IncrementNonceFunction incrementNonceFunction)
        {
             return ContractHandler.SendRequestAsync(incrementNonceFunction);
        }

        public virtual Task<TransactionReceipt> IncrementNonceRequestAndWaitForReceiptAsync(IncrementNonceFunction incrementNonceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(incrementNonceFunction, cancellationToken);
        }

        public virtual Task<string> IncrementNonceRequestAsync(BigInteger key)
        {
            var incrementNonceFunction = new IncrementNonceFunction();
                incrementNonceFunction.Key = key;
            
             return ContractHandler.SendRequestAsync(incrementNonceFunction);
        }

        public virtual Task<TransactionReceipt> IncrementNonceRequestAndWaitForReceiptAsync(BigInteger key, CancellationTokenSource cancellationToken = null)
        {
            var incrementNonceFunction = new IncrementNonceFunction();
                incrementNonceFunction.Key = key;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(incrementNonceFunction, cancellationToken);
        }

        public virtual Task<string> InnerHandleOpRequestAsync(InnerHandleOpFunction innerHandleOpFunction)
        {
             return ContractHandler.SendRequestAsync(innerHandleOpFunction);
        }

        public virtual Task<TransactionReceipt> InnerHandleOpRequestAndWaitForReceiptAsync(InnerHandleOpFunction innerHandleOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(innerHandleOpFunction, cancellationToken);
        }

        public virtual Task<string> InnerHandleOpRequestAsync(byte[] callData, UserOpInfo opInfo, byte[] context)
        {
            var innerHandleOpFunction = new InnerHandleOpFunction();
                innerHandleOpFunction.CallData = callData;
                innerHandleOpFunction.OpInfo = opInfo;
                innerHandleOpFunction.Context = context;
            
             return ContractHandler.SendRequestAsync(innerHandleOpFunction);
        }

        public virtual Task<TransactionReceipt> InnerHandleOpRequestAndWaitForReceiptAsync(byte[] callData, UserOpInfo opInfo, byte[] context, CancellationTokenSource cancellationToken = null)
        {
            var innerHandleOpFunction = new InnerHandleOpFunction();
                innerHandleOpFunction.CallData = callData;
                innerHandleOpFunction.OpInfo = opInfo;
                innerHandleOpFunction.Context = context;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(innerHandleOpFunction, cancellationToken);
        }

        public Task<BigInteger> NonceSequenceNumberQueryAsync(NonceSequenceNumberFunction nonceSequenceNumberFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceSequenceNumberFunction, BigInteger>(nonceSequenceNumberFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceSequenceNumberQueryAsync(string returnValue1, BigInteger returnValue2, BlockParameter blockParameter = null)
        {
            var nonceSequenceNumberFunction = new NonceSequenceNumberFunction();
                nonceSequenceNumberFunction.ReturnValue1 = returnValue1;
                nonceSequenceNumberFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<NonceSequenceNumberFunction, BigInteger>(nonceSequenceNumberFunction, blockParameter);
        }

        public Task<string> SenderCreatorQueryAsync(SenderCreatorFunction senderCreatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SenderCreatorFunction, string>(senderCreatorFunction, blockParameter);
        }

        
        public virtual Task<string> SenderCreatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SenderCreatorFunction, string>(null, blockParameter);
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

        public virtual Task<string> UnlockStakeRequestAsync(UnlockStakeFunction unlockStakeFunction)
        {
             return ContractHandler.SendRequestAsync(unlockStakeFunction);
        }

        public virtual Task<string> UnlockStakeRequestAsync()
        {
             return ContractHandler.SendRequestAsync<UnlockStakeFunction>();
        }

        public virtual Task<TransactionReceipt> UnlockStakeRequestAndWaitForReceiptAsync(UnlockStakeFunction unlockStakeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockStakeFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> UnlockStakeRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<UnlockStakeFunction>(null, cancellationToken);
        }

        public virtual Task<string> WithdrawStakeRequestAsync(WithdrawStakeFunction withdrawStakeFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawStakeFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawStakeRequestAndWaitForReceiptAsync(WithdrawStakeFunction withdrawStakeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawStakeFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawStakeRequestAsync(string withdrawAddress)
        {
            var withdrawStakeFunction = new WithdrawStakeFunction();
                withdrawStakeFunction.WithdrawAddress = withdrawAddress;
            
             return ContractHandler.SendRequestAsync(withdrawStakeFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawStakeRequestAndWaitForReceiptAsync(string withdrawAddress, CancellationTokenSource cancellationToken = null)
        {
            var withdrawStakeFunction = new WithdrawStakeFunction();
                withdrawStakeFunction.WithdrawAddress = withdrawAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawStakeFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(WithdrawToFunction withdrawToFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(WithdrawToFunction withdrawToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(string withdrawAddress, BigInteger withdrawAmount)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.WithdrawAddress = withdrawAddress;
                withdrawToFunction.WithdrawAmount = withdrawAmount;
            
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(string withdrawAddress, BigInteger withdrawAmount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.WithdrawAddress = withdrawAddress;
                withdrawToFunction.WithdrawAmount = withdrawAmount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AddStakeFunction),
                typeof(BalanceOfFunction),
                typeof(DelegateAndRevertFunction),
                typeof(DepositToFunction),
                typeof(Eip712DomainFunction),
                typeof(GetDepositInfoFunction),
                typeof(GetDomainSeparatorV4Function),
                typeof(GetNonceFunction),
                typeof(GetPackedUserOpTypeHashFunction),
                typeof(GetSenderAddressFunction),
                typeof(GetUserOpHashFunction),
                typeof(HandleAggregatedOpsFunction),
                typeof(HandleOpsFunction),
                typeof(IncrementNonceFunction),
                typeof(InnerHandleOpFunction),
                typeof(NonceSequenceNumberFunction),
                typeof(SenderCreatorFunction),
                typeof(SupportsInterfaceFunction),
                typeof(UnlockStakeFunction),
                typeof(WithdrawStakeFunction),
                typeof(WithdrawToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountDeployedEventDTO),
                typeof(BeforeExecutionEventDTO),
                typeof(DepositedEventDTO),
                typeof(EIP712DomainChangedEventDTO),
                typeof(PostOpRevertReasonEventDTO),
                typeof(SignatureAggregatorChangedEventDTO),
                typeof(StakeLockedEventDTO),
                typeof(StakeUnlockedEventDTO),
                typeof(StakeWithdrawnEventDTO),
                typeof(UserOperationEventEventDTO),
                typeof(UserOperationPrefundTooLowEventDTO),
                typeof(UserOperationRevertReasonEventDTO),
                typeof(WithdrawnEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(DelegateAndRevertError),
                typeof(FailedOpError),
                typeof(FailedOpWithRevertError),
                typeof(InvalidShortStringError),
                typeof(PostOpRevertedError),
                typeof(ReentrancyGuardReentrantCallError),
                typeof(SenderAddressResultError),
                typeof(SignatureValidationFailedError),
                typeof(StringTooLongError)
            };
        }
    }
}
