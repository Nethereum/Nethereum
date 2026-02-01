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
using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster
{
    public partial class DepositPaymasterService: DepositPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, DepositPaymasterDeployment depositPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<DepositPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(depositPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, DepositPaymasterDeployment depositPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DepositPaymasterDeployment>().SendRequestAsync(depositPaymasterDeployment);
        }

        public static async Task<DepositPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, DepositPaymasterDeployment depositPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, depositPaymasterDeployment, cancellationTokenSource);
            return new DepositPaymasterService(web3, receipt.ContractAddress);
        }

        public DepositPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class DepositPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public DepositPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> DepositRequestAsync(DepositFunction depositFunction)
        {
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<string> DepositRequestAsync()
        {
             return ContractHandler.SendRequestAsync<DepositFunction>();
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(DepositFunction depositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<DepositFunction>(null, cancellationToken);
        }

        public virtual Task<string> DepositForRequestAsync(DepositForFunction depositForFunction)
        {
             return ContractHandler.SendRequestAsync(depositForFunction);
        }

        public virtual Task<TransactionReceipt> DepositForRequestAndWaitForReceiptAsync(DepositForFunction depositForFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositForFunction, cancellationToken);
        }

        public virtual Task<string> DepositForRequestAsync(string account)
        {
            var depositForFunction = new DepositForFunction();
                depositForFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(depositForFunction);
        }

        public virtual Task<TransactionReceipt> DepositForRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var depositForFunction = new DepositForFunction();
                depositForFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositForFunction, cancellationToken);
        }

        public Task<BigInteger> DepositsQueryAsync(DepositsFunction depositsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DepositsFunction, BigInteger>(depositsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DepositsQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var depositsFunction = new DepositsFunction();
                depositsFunction.Account = account;
            
            return ContractHandler.QueryAsync<DepositsFunction, BigInteger>(depositsFunction, blockParameter);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
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

        public Task<BigInteger> MinDepositQueryAsync(MinDepositFunction minDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinDepositFunction, BigInteger>(minDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MinDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinDepositFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> PostOpRequestAsync(PostOpFunction postOpFunction)
        {
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(PostOpFunction postOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> PostOpRequestAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger returnValue4)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ReturnValue4 = returnValue4;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger returnValue4, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ReturnValue4 = returnValue4;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public virtual Task<string> RenounceOwnershipRequestAsync()
        {
             return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public virtual Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public virtual Task<string> SetMinDepositRequestAsync(SetMinDepositFunction setMinDepositFunction)
        {
             return ContractHandler.SendRequestAsync(setMinDepositFunction);
        }

        public virtual Task<TransactionReceipt> SetMinDepositRequestAndWaitForReceiptAsync(SetMinDepositFunction setMinDepositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMinDepositFunction, cancellationToken);
        }

        public virtual Task<string> SetMinDepositRequestAsync(BigInteger min)
        {
            var setMinDepositFunction = new SetMinDepositFunction();
                setMinDepositFunction.Min = min;
            
             return ContractHandler.SendRequestAsync(setMinDepositFunction);
        }

        public virtual Task<TransactionReceipt> SetMinDepositRequestAndWaitForReceiptAsync(BigInteger min, CancellationTokenSource cancellationToken = null)
        {
            var setMinDepositFunction = new SetMinDepositFunction();
                setMinDepositFunction.Min = min;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMinDepositFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(PackedUserOperation userOp, byte[] returnValue2, BigInteger maxCost)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.ReturnValue2 = returnValue2;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] returnValue2, BigInteger maxCost, CancellationTokenSource cancellationToken = null)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.ReturnValue2 = returnValue2;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawRequestAsync(BigInteger amount)
        {
            var withdrawFunction = new WithdrawFunction();
                withdrawFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawFunction = new WithdrawFunction();
                withdrawFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(WithdrawToFunction withdrawToFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(WithdrawToFunction withdrawToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(string to, BigInteger amount)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.To = to;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.To = to;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DepositFunction),
                typeof(DepositForFunction),
                typeof(DepositsFunction),
                typeof(EntryPointFunction),
                typeof(GetDepositFunction),
                typeof(GetDepositInfoFunction),
                typeof(MinDepositFunction),
                typeof(OwnerFunction),
                typeof(PostOpFunction),
                typeof(RenounceOwnershipFunction),
                typeof(SetMinDepositFunction),
                typeof(TransferOwnershipFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(WithdrawFunction),
                typeof(WithdrawToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(DepositedEventDTO),
                typeof(MinDepositChangedEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(WithdrawnEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(InsufficientDepositError),
                typeof(InsufficientUserDepositError),
                typeof(OnlyEntryPointError),
                typeof(OwnableInvalidOwnerError),
                typeof(OwnableUnauthorizedAccountError),
                typeof(ReentrancyGuardReentrantCallError),
                typeof(WithdrawFailedError)
            };
        }
    }
}
