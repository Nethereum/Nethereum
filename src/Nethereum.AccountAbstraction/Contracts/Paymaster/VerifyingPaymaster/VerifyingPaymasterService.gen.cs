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
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster
{
    public partial class VerifyingPaymasterService: VerifyingPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, VerifyingPaymasterDeployment verifyingPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<VerifyingPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(verifyingPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, VerifyingPaymasterDeployment verifyingPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<VerifyingPaymasterDeployment>().SendRequestAsync(verifyingPaymasterDeployment);
        }

        public static async Task<VerifyingPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, VerifyingPaymasterDeployment verifyingPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, verifyingPaymasterDeployment, cancellationTokenSource);
            return new VerifyingPaymasterService(web3, receipt.ContractAddress);
        }

        public VerifyingPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class VerifyingPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public VerifyingPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public Task<byte[]> GetHashQueryAsync(GetHashFunction getHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetHashFunction, byte[]>(getHashFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetHashQueryAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter, BlockParameter blockParameter = null)
        {
            var getHashFunction = new GetHashFunction();
                getHashFunction.UserOp = userOp;
                getHashFunction.ValidUntil = validUntil;
                getHashFunction.ValidAfter = validAfter;
            
            return ContractHandler.QueryAsync<GetHashFunction, byte[]>(getHashFunction, blockParameter);
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

        public virtual Task<string> PostOpRequestAsync(byte returnValue1, byte[] context, BigInteger actualGasCost, BigInteger returnValue4)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.ReturnValue1 = returnValue1;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ReturnValue4 = returnValue4;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte returnValue1, byte[] context, BigInteger actualGasCost, BigInteger returnValue4, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.ReturnValue1 = returnValue1;
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

        public Task<BigInteger> SenderNonceQueryAsync(SenderNonceFunction senderNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SenderNonceFunction, BigInteger>(senderNonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> SenderNonceQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var senderNonceFunction = new SenderNonceFunction();
                senderNonceFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<SenderNonceFunction, BigInteger>(senderNonceFunction, blockParameter);
        }

        public virtual Task<string> SetVerifyingSignerRequestAsync(SetVerifyingSignerFunction setVerifyingSignerFunction)
        {
             return ContractHandler.SendRequestAsync(setVerifyingSignerFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifyingSignerRequestAndWaitForReceiptAsync(SetVerifyingSignerFunction setVerifyingSignerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifyingSignerFunction, cancellationToken);
        }

        public virtual Task<string> SetVerifyingSignerRequestAsync(string signer)
        {
            var setVerifyingSignerFunction = new SetVerifyingSignerFunction();
                setVerifyingSignerFunction.Signer = signer;
            
             return ContractHandler.SendRequestAsync(setVerifyingSignerFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifyingSignerRequestAndWaitForReceiptAsync(string signer, CancellationTokenSource cancellationToken = null)
        {
            var setVerifyingSignerFunction = new SetVerifyingSignerFunction();
                setVerifyingSignerFunction.Signer = signer;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifyingSignerFunction, cancellationToken);
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

        public Task<string> VerifyingSignerQueryAsync(VerifyingSignerFunction verifyingSignerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyingSignerFunction, string>(verifyingSignerFunction, blockParameter);
        }

        
        public virtual Task<string> VerifyingSignerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyingSignerFunction, string>(null, blockParameter);
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
                typeof(EntryPointFunction),
                typeof(GetDepositFunction),
                typeof(GetHashFunction),
                typeof(OwnerFunction),
                typeof(PostOpFunction),
                typeof(RenounceOwnershipFunction),
                typeof(SenderNonceFunction),
                typeof(SetVerifyingSignerFunction),
                typeof(TransferOwnershipFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(VerifyingSignerFunction),
                typeof(WithdrawToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(GasSponsoredEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(SignerChangedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(ExpiredSignatureError),
                typeof(InsufficientDepositError),
                typeof(InvalidSignatureError),
                typeof(OnlyEntryPointError),
                typeof(OwnableInvalidOwnerError),
                typeof(OwnableUnauthorizedAccountError)
            };
        }
    }
}
