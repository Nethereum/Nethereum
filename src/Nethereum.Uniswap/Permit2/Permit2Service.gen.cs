using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;
using Nethereum.Uniswap.Permit2.ContractDefinition;

namespace Nethereum.Uniswap.Permit2
{
    public partial class Permit2Service: Permit2ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, Permit2Deployment permit2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<Permit2Deployment>().SendRequestAndWaitForReceiptAsync(permit2Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, Permit2Deployment permit2Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<Permit2Deployment>().SendRequestAsync(permit2Deployment);
        }

        public static async Task<Permit2Service> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, Permit2Deployment permit2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, permit2Deployment, cancellationTokenSource);
            return new Permit2Service(web3, receipt.ContractAddress);
        }

        public Permit2Service(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class Permit2ServiceBase: ContractWeb3ServiceBase
    {

        public Permit2ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> DomainSeparatorQueryAsync(DomainSeparatorFunction domainSeparatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(domainSeparatorFunction, blockParameter);
        }

        
        public virtual Task<byte[]> DomainSeparatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<AllowanceOutputDTO> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AllowanceFunction, AllowanceOutputDTO>(allowanceFunction, blockParameter);
        }

        public virtual Task<AllowanceOutputDTO> AllowanceQueryAsync(string returnValue1, string returnValue2, string returnValue3, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.ReturnValue1 = returnValue1;
                allowanceFunction.ReturnValue2 = returnValue2;
                allowanceFunction.ReturnValue3 = returnValue3;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AllowanceFunction, AllowanceOutputDTO>(allowanceFunction, blockParameter);
        }

        public virtual Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public virtual Task<string> ApproveRequestAsync(string token, string spender, BigInteger amount, ulong expiration)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Token = token;
                approveFunction.Spender = spender;
                approveFunction.Amount = amount;
                approveFunction.Expiration = expiration;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string token, string spender, BigInteger amount, ulong expiration, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Token = token;
                approveFunction.Spender = spender;
                approveFunction.Amount = amount;
                approveFunction.Expiration = expiration;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<byte[]> HashPermitSingleQueryAsync(HashPermitSingleFunction hashPermitSingleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashPermitSingleFunction, byte[]>(hashPermitSingleFunction, blockParameter);
        }

        
        public virtual Task<byte[]> HashPermitSingleQueryAsync(PermitSingle permitSingle, BlockParameter blockParameter = null)
        {
            var hashPermitSingleFunction = new HashPermitSingleFunction();
                hashPermitSingleFunction.PermitSingle = permitSingle;
            
            return ContractHandler.QueryAsync<HashPermitSingleFunction, byte[]>(hashPermitSingleFunction, blockParameter);
        }

        public virtual Task<string> InvalidateNoncesRequestAsync(InvalidateNoncesFunction invalidateNoncesFunction)
        {
             return ContractHandler.SendRequestAsync(invalidateNoncesFunction);
        }

        public virtual Task<TransactionReceipt> InvalidateNoncesRequestAndWaitForReceiptAsync(InvalidateNoncesFunction invalidateNoncesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateNoncesFunction, cancellationToken);
        }

        public virtual Task<string> InvalidateNoncesRequestAsync(string token, string spender, ulong newNonce)
        {
            var invalidateNoncesFunction = new InvalidateNoncesFunction();
                invalidateNoncesFunction.Token = token;
                invalidateNoncesFunction.Spender = spender;
                invalidateNoncesFunction.NewNonce = newNonce;
            
             return ContractHandler.SendRequestAsync(invalidateNoncesFunction);
        }

        public virtual Task<TransactionReceipt> InvalidateNoncesRequestAndWaitForReceiptAsync(string token, string spender, ulong newNonce, CancellationTokenSource cancellationToken = null)
        {
            var invalidateNoncesFunction = new InvalidateNoncesFunction();
                invalidateNoncesFunction.Token = token;
                invalidateNoncesFunction.Spender = spender;
                invalidateNoncesFunction.NewNonce = newNonce;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateNoncesFunction, cancellationToken);
        }

        public virtual Task<string> InvalidateUnorderedNoncesRequestAsync(InvalidateUnorderedNoncesFunction invalidateUnorderedNoncesFunction)
        {
             return ContractHandler.SendRequestAsync(invalidateUnorderedNoncesFunction);
        }

        public virtual Task<TransactionReceipt> InvalidateUnorderedNoncesRequestAndWaitForReceiptAsync(InvalidateUnorderedNoncesFunction invalidateUnorderedNoncesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateUnorderedNoncesFunction, cancellationToken);
        }

        public virtual Task<string> InvalidateUnorderedNoncesRequestAsync(BigInteger wordPos, BigInteger mask)
        {
            var invalidateUnorderedNoncesFunction = new InvalidateUnorderedNoncesFunction();
                invalidateUnorderedNoncesFunction.WordPos = wordPos;
                invalidateUnorderedNoncesFunction.Mask = mask;
            
             return ContractHandler.SendRequestAsync(invalidateUnorderedNoncesFunction);
        }

        public virtual Task<TransactionReceipt> InvalidateUnorderedNoncesRequestAndWaitForReceiptAsync(BigInteger wordPos, BigInteger mask, CancellationTokenSource cancellationToken = null)
        {
            var invalidateUnorderedNoncesFunction = new InvalidateUnorderedNoncesFunction();
                invalidateUnorderedNoncesFunction.WordPos = wordPos;
                invalidateUnorderedNoncesFunction.Mask = mask;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateUnorderedNoncesFunction, cancellationToken);
        }

        public virtual Task<string> LockdownRequestAsync(LockdownFunction lockdownFunction)
        {
             return ContractHandler.SendRequestAsync(lockdownFunction);
        }

        public virtual Task<TransactionReceipt> LockdownRequestAndWaitForReceiptAsync(LockdownFunction lockdownFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(lockdownFunction, cancellationToken);
        }

        public virtual Task<string> LockdownRequestAsync(List<TokenSpenderPair> approvals)
        {
            var lockdownFunction = new LockdownFunction();
                lockdownFunction.Approvals = approvals;
            
             return ContractHandler.SendRequestAsync(lockdownFunction);
        }

        public virtual Task<TransactionReceipt> LockdownRequestAndWaitForReceiptAsync(List<TokenSpenderPair> approvals, CancellationTokenSource cancellationToken = null)
        {
            var lockdownFunction = new LockdownFunction();
                lockdownFunction.Approvals = approvals;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(lockdownFunction, cancellationToken);
        }

        public Task<BigInteger> NonceBitmapQueryAsync(NonceBitmapFunction nonceBitmapFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceBitmapFunction, BigInteger>(nonceBitmapFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceBitmapQueryAsync(string returnValue1, BigInteger returnValue2, BlockParameter blockParameter = null)
        {
            var nonceBitmapFunction = new NonceBitmapFunction();
                nonceBitmapFunction.ReturnValue1 = returnValue1;
                nonceBitmapFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<NonceBitmapFunction, BigInteger>(nonceBitmapFunction, blockParameter);
        }

        public virtual Task<string> PermitRequestAsync(PermitFunction permitFunction)
        {
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(PermitFunction permitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(string owner, PermitBatch permitBatch, byte[] signature)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.PermitBatch = permitBatch;
                permitFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string owner, PermitBatch permitBatch, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.PermitBatch = permitBatch;
                permitFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(Permit1Function permit1Function)
        {
             return ContractHandler.SendRequestAsync(permit1Function);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(Permit1Function permit1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permit1Function, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(string owner, PermitSingle permitSingle, byte[] signature)
        {
            var permit1Function = new Permit1Function();
                permit1Function.Owner = owner;
                permit1Function.PermitSingle = permitSingle;
                permit1Function.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permit1Function);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string owner, PermitSingle permitSingle, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permit1Function = new Permit1Function();
                permit1Function.Owner = owner;
                permit1Function.PermitSingle = permitSingle;
                permit1Function.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permit1Function, cancellationToken);
        }

        public virtual Task<string> PermitTransferFromRequestAsync(PermitTransferFromFunction permitTransferFromFunction)
        {
             return ContractHandler.SendRequestAsync(permitTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> PermitTransferFromRequestAndWaitForReceiptAsync(PermitTransferFromFunction permitTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> PermitTransferFromRequestAsync(PermitTransferFrom permit, SignatureTransferDetails transferDetails, string owner, byte[] signature)
        {
            var permitTransferFromFunction = new PermitTransferFromFunction();
                permitTransferFromFunction.Permit = permit;
                permitTransferFromFunction.TransferDetails = transferDetails;
                permitTransferFromFunction.Owner = owner;
                permitTransferFromFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> PermitTransferFromRequestAndWaitForReceiptAsync(PermitTransferFrom permit, SignatureTransferDetails transferDetails, string owner, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitTransferFromFunction = new PermitTransferFromFunction();
                permitTransferFromFunction.Permit = permit;
                permitTransferFromFunction.TransferDetails = transferDetails;
                permitTransferFromFunction.Owner = owner;
                permitTransferFromFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> PermitTransferFromRequestAsync(PermitTransferFrom1Function permitTransferFrom1Function)
        {
             return ContractHandler.SendRequestAsync(permitTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> PermitTransferFromRequestAndWaitForReceiptAsync(PermitTransferFrom1Function permitTransferFrom1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> PermitTransferFromRequestAsync(PermitBatchTransferFrom permit, List<SignatureTransferDetails> transferDetails, string owner, byte[] signature)
        {
            var permitTransferFrom1Function = new PermitTransferFrom1Function();
                permitTransferFrom1Function.Permit = permit;
                permitTransferFrom1Function.TransferDetails = transferDetails;
                permitTransferFrom1Function.Owner = owner;
                permitTransferFrom1Function.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> PermitTransferFromRequestAndWaitForReceiptAsync(PermitBatchTransferFrom permit, List<SignatureTransferDetails> transferDetails, string owner, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitTransferFrom1Function = new PermitTransferFrom1Function();
                permitTransferFrom1Function.Permit = permit;
                permitTransferFrom1Function.TransferDetails = transferDetails;
                permitTransferFrom1Function.Owner = owner;
                permitTransferFrom1Function.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> PermitWitnessTransferFromRequestAsync(PermitWitnessTransferFromFunction permitWitnessTransferFromFunction)
        {
             return ContractHandler.SendRequestAsync(permitWitnessTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> PermitWitnessTransferFromRequestAndWaitForReceiptAsync(PermitWitnessTransferFromFunction permitWitnessTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitWitnessTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> PermitWitnessTransferFromRequestAsync(PermitTransferFrom permit, SignatureTransferDetails transferDetails, string owner, byte[] witness, string witnessTypeString, byte[] signature)
        {
            var permitWitnessTransferFromFunction = new PermitWitnessTransferFromFunction();
                permitWitnessTransferFromFunction.Permit = permit;
                permitWitnessTransferFromFunction.TransferDetails = transferDetails;
                permitWitnessTransferFromFunction.Owner = owner;
                permitWitnessTransferFromFunction.Witness = witness;
                permitWitnessTransferFromFunction.WitnessTypeString = witnessTypeString;
                permitWitnessTransferFromFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitWitnessTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> PermitWitnessTransferFromRequestAndWaitForReceiptAsync(PermitTransferFrom permit, SignatureTransferDetails transferDetails, string owner, byte[] witness, string witnessTypeString, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitWitnessTransferFromFunction = new PermitWitnessTransferFromFunction();
                permitWitnessTransferFromFunction.Permit = permit;
                permitWitnessTransferFromFunction.TransferDetails = transferDetails;
                permitWitnessTransferFromFunction.Owner = owner;
                permitWitnessTransferFromFunction.Witness = witness;
                permitWitnessTransferFromFunction.WitnessTypeString = witnessTypeString;
                permitWitnessTransferFromFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitWitnessTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> PermitWitnessTransferFromRequestAsync(PermitWitnessTransferFrom1Function permitWitnessTransferFrom1Function)
        {
             return ContractHandler.SendRequestAsync(permitWitnessTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> PermitWitnessTransferFromRequestAndWaitForReceiptAsync(PermitWitnessTransferFrom1Function permitWitnessTransferFrom1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitWitnessTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> PermitWitnessTransferFromRequestAsync(PermitBatchTransferFrom permit, List<SignatureTransferDetails> transferDetails, string owner, byte[] witness, string witnessTypeString, byte[] signature)
        {
            var permitWitnessTransferFrom1Function = new PermitWitnessTransferFrom1Function();
                permitWitnessTransferFrom1Function.Permit = permit;
                permitWitnessTransferFrom1Function.TransferDetails = transferDetails;
                permitWitnessTransferFrom1Function.Owner = owner;
                permitWitnessTransferFrom1Function.Witness = witness;
                permitWitnessTransferFrom1Function.WitnessTypeString = witnessTypeString;
                permitWitnessTransferFrom1Function.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitWitnessTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> PermitWitnessTransferFromRequestAndWaitForReceiptAsync(PermitBatchTransferFrom permit, List<SignatureTransferDetails> transferDetails, string owner, byte[] witness, string witnessTypeString, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitWitnessTransferFrom1Function = new PermitWitnessTransferFrom1Function();
                permitWitnessTransferFrom1Function.Permit = permit;
                permitWitnessTransferFrom1Function.TransferDetails = transferDetails;
                permitWitnessTransferFrom1Function.Owner = owner;
                permitWitnessTransferFrom1Function.Witness = witness;
                permitWitnessTransferFrom1Function.WitnessTypeString = witnessTypeString;
                permitWitnessTransferFrom1Function.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitWitnessTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(List<AllowanceTransferDetails> transferDetails)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.TransferDetails = transferDetails;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(List<AllowanceTransferDetails> transferDetails, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.TransferDetails = transferDetails;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(TransferFrom1Function transferFrom1Function)
        {
             return ContractHandler.SendRequestAsync(transferFrom1Function);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFrom1Function transferFrom1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFrom1Function, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(string from, string to, BigInteger amount, string token)
        {
            var transferFrom1Function = new TransferFrom1Function();
                transferFrom1Function.From = from;
                transferFrom1Function.To = to;
                transferFrom1Function.Amount = amount;
                transferFrom1Function.Token = token;
            
             return ContractHandler.SendRequestAsync(transferFrom1Function);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger amount, string token, CancellationTokenSource cancellationToken = null)
        {
            var transferFrom1Function = new TransferFrom1Function();
                transferFrom1Function.From = from;
                transferFrom1Function.To = to;
                transferFrom1Function.Amount = amount;
                transferFrom1Function.Token = token;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFrom1Function, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DomainSeparatorFunction),
                typeof(AllowanceFunction),
                typeof(ApproveFunction),
                typeof(HashPermitSingleFunction),
                typeof(InvalidateNoncesFunction),
                typeof(InvalidateUnorderedNoncesFunction),
                typeof(LockdownFunction),
                typeof(NonceBitmapFunction),
                typeof(PermitFunction),
                typeof(Permit1Function),
                typeof(PermitTransferFromFunction),
                typeof(PermitTransferFrom1Function),
                typeof(PermitWitnessTransferFromFunction),
                typeof(PermitWitnessTransferFrom1Function),
                typeof(TransferFromFunction),
                typeof(TransferFrom1Function)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalEventDTO),
                typeof(LockdownEventDTO),
                typeof(NonceInvalidationEventDTO),
                typeof(PermitEventDTO),
                typeof(UnorderedNonceInvalidationEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AllowanceExpiredError),
                typeof(ExcessiveInvalidationError),
                typeof(InsufficientAllowanceError),
                typeof(InvalidAmountError),
                typeof(InvalidContractSignatureError),
                typeof(InvalidNonceError),
                typeof(InvalidSignatureError),
                typeof(InvalidSignatureLengthError),
                typeof(InvalidSignerError),
                typeof(LengthMismatchError),
                typeof(SignatureExpiredError)
            };
        }
    }
}
