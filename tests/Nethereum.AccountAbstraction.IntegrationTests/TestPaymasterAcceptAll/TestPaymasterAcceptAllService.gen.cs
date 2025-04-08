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
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll
{
    public partial class TestPaymasterAcceptAllService: TestPaymasterAcceptAllServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TestPaymasterAcceptAllDeployment testPaymasterAcceptAllDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestPaymasterAcceptAllDeployment>().SendRequestAndWaitForReceiptAsync(testPaymasterAcceptAllDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TestPaymasterAcceptAllDeployment testPaymasterAcceptAllDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestPaymasterAcceptAllDeployment>().SendRequestAsync(testPaymasterAcceptAllDeployment);
        }

        public static async Task<TestPaymasterAcceptAllService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TestPaymasterAcceptAllDeployment testPaymasterAcceptAllDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testPaymasterAcceptAllDeployment, cancellationTokenSource);
            return new TestPaymasterAcceptAllService(web3, receipt.ContractAddress);
        }

        public TestPaymasterAcceptAllService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TestPaymasterAcceptAllServiceBase: ContractWeb3ServiceBase
    {

        public TestPaymasterAcceptAllServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AcceptOwnershipRequestAsync(AcceptOwnershipFunction acceptOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(acceptOwnershipFunction);
        }

        public virtual Task<string> AcceptOwnershipRequestAsync()
        {
             return ContractHandler.SendRequestAsync<AcceptOwnershipFunction>();
        }

        public virtual Task<TransactionReceipt> AcceptOwnershipRequestAndWaitForReceiptAsync(AcceptOwnershipFunction acceptOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acceptOwnershipFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> AcceptOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<AcceptOwnershipFunction>(null, cancellationToken);
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

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> PendingOwnerQueryAsync(PendingOwnerFunction pendingOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PendingOwnerFunction, string>(pendingOwnerFunction, blockParameter);
        }

        
        public virtual Task<string> PendingOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PendingOwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> PostOpRequestAsync(PostOpFunction postOpFunction)
        {
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(PostOpFunction postOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> PostOpRequestAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
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

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost, CancellationTokenSource cancellationToken = null)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
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

        public virtual Task<string> WithdrawToRequestAsync(string withdrawAddress, BigInteger amount)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.WithdrawAddress = withdrawAddress;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(string withdrawAddress, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.WithdrawAddress = withdrawAddress;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AcceptOwnershipFunction),
                typeof(AddStakeFunction),
                typeof(DepositFunction),
                typeof(EntryPointFunction),
                typeof(GetDepositFunction),
                typeof(OwnerFunction),
                typeof(PendingOwnerFunction),
                typeof(PostOpFunction),
                typeof(RenounceOwnershipFunction),
                typeof(TransferOwnershipFunction),
                typeof(UnlockStakeFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(WithdrawStakeFunction),
                typeof(WithdrawToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(OwnershipTransferStartedEventDTO),
                typeof(OwnershipTransferredEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(OwnableInvalidOwnerError),
                typeof(OwnableUnauthorizedAccountError)
            };
        }
    }
}
