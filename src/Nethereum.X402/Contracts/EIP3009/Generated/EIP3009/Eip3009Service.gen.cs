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
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;

namespace Nethereum.Contracts.EIP3009.EIP3009
{
    public partial class Eip3009Service: Eip3009ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, Eip3009Deployment eip3009Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<Eip3009Deployment>().SendRequestAndWaitForReceiptAsync(eip3009Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, Eip3009Deployment eip3009Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<Eip3009Deployment>().SendRequestAsync(eip3009Deployment);
        }

        public static async Task<Eip3009Service> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, Eip3009Deployment eip3009Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eip3009Deployment, cancellationTokenSource);
            return new Eip3009Service(web3, receipt.ContractAddress);
        }

        public Eip3009Service(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class Eip3009ServiceBase: ContractWeb3ServiceBase
    {

        public Eip3009ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> TransferWithAuthorizationRequestAsync(TransferWithAuthorization1Function transferWithAuthorization1Function)
        {
             return ContractHandler.SendRequestAsync(transferWithAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> TransferWithAuthorizationRequestAndWaitForReceiptAsync(TransferWithAuthorization1Function transferWithAuthorization1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferWithAuthorization1Function, cancellationToken);
        }

        public virtual Task<string> TransferWithAuthorizationRequestAsync(string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore, byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var transferWithAuthorization1Function = new TransferWithAuthorization1Function();
                transferWithAuthorization1Function.AuthorisationFrom = from;
                transferWithAuthorization1Function.AuthorisationTo = to;
                transferWithAuthorization1Function.Value = value;
                transferWithAuthorization1Function.ValidAfter = validAfter;
                transferWithAuthorization1Function.ValidBefore = validBefore;
                transferWithAuthorization1Function.AuthorisationNonce = nonce;
                transferWithAuthorization1Function.V = v;
                transferWithAuthorization1Function.R = r;
                transferWithAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAsync(transferWithAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> TransferWithAuthorizationRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore, byte[] nonce, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var transferWithAuthorization1Function = new TransferWithAuthorization1Function();
                transferWithAuthorization1Function.AuthorisationFrom = from;
                transferWithAuthorization1Function.AuthorisationTo = to;
                transferWithAuthorization1Function.Value = value;
                transferWithAuthorization1Function.ValidAfter = validAfter;
                transferWithAuthorization1Function.ValidBefore = validBefore;
                transferWithAuthorization1Function.AuthorisationNonce = nonce;
                transferWithAuthorization1Function.V = v;
                transferWithAuthorization1Function.R = r;
                transferWithAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferWithAuthorization1Function, cancellationToken);
        }

        public virtual Task<string> ReceiveWithAuthorizationRequestAsync(ReceiveWithAuthorization1Function receiveWithAuthorization1Function)
        {
             return ContractHandler.SendRequestAsync(receiveWithAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(ReceiveWithAuthorization1Function receiveWithAuthorization1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(receiveWithAuthorization1Function, cancellationToken);
        }

        public virtual Task<string> ReceiveWithAuthorizationRequestAsync(string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore, byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var receiveWithAuthorization1Function = new ReceiveWithAuthorization1Function();
                receiveWithAuthorization1Function.From = from;
                receiveWithAuthorization1Function.To = to;
                receiveWithAuthorization1Function.Value = value;
                receiveWithAuthorization1Function.ValidAfter = validAfter;
                receiveWithAuthorization1Function.ValidBefore = validBefore;
                receiveWithAuthorization1Function.AuthorisationNonce = nonce;
                receiveWithAuthorization1Function.V = v;
                receiveWithAuthorization1Function.R = r;
                receiveWithAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAsync(receiveWithAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore, byte[] nonce, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var receiveWithAuthorization1Function = new ReceiveWithAuthorization1Function();
                receiveWithAuthorization1Function.From = from;
                receiveWithAuthorization1Function.To = to;
                receiveWithAuthorization1Function.Value = value;
                receiveWithAuthorization1Function.ValidAfter = validAfter;
                receiveWithAuthorization1Function.ValidBefore = validBefore;
                receiveWithAuthorization1Function.AuthorisationNonce = nonce;
                receiveWithAuthorization1Function.V = v;
                receiveWithAuthorization1Function.R = r;
                receiveWithAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(receiveWithAuthorization1Function, cancellationToken);
        }

        public virtual Task<string> CancelAuthorizationRequestAsync(CancelAuthorization1Function cancelAuthorization1Function)
        {
             return ContractHandler.SendRequestAsync(cancelAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> CancelAuthorizationRequestAndWaitForReceiptAsync(CancelAuthorization1Function cancelAuthorization1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelAuthorization1Function, cancellationToken);
        }

        public virtual Task<string> CancelAuthorizationRequestAsync(string authorizer, byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var cancelAuthorization1Function = new CancelAuthorization1Function();
                cancelAuthorization1Function.Authorizer = authorizer;
                cancelAuthorization1Function.AuthorisationNonce = nonce;
                cancelAuthorization1Function.V = v;
                cancelAuthorization1Function.R = r;
                cancelAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAsync(cancelAuthorization1Function);
        }

        public virtual Task<TransactionReceipt> CancelAuthorizationRequestAndWaitForReceiptAsync(string authorizer, byte[] nonce, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var cancelAuthorization1Function = new CancelAuthorization1Function();
                cancelAuthorization1Function.Authorizer = authorizer;
                cancelAuthorization1Function.AuthorisationNonce = nonce;
                cancelAuthorization1Function.V = v;
                cancelAuthorization1Function.R = r;
                cancelAuthorization1Function.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelAuthorization1Function, cancellationToken);
        }

        public Task<bool> AuthorizationStateQueryAsync(AuthorizationStateFunction authorizationStateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AuthorizationStateFunction, bool>(authorizationStateFunction, blockParameter);
        }

        
        public virtual Task<bool> AuthorizationStateQueryAsync(string authorizer, byte[] nonce, BlockParameter blockParameter = null)
        {
            var authorizationStateFunction = new AuthorizationStateFunction();
                authorizationStateFunction.Authorizer = authorizer;
                authorizationStateFunction.AuthorisationNonce = nonce;
            
            return ContractHandler.QueryAsync<AuthorizationStateFunction, bool>(authorizationStateFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(TransferWithAuthorization1Function),
                typeof(ReceiveWithAuthorization1Function),
                typeof(CancelAuthorization1Function),
                typeof(AuthorizationStateFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AuthorizationUsedEventDTO),
                typeof(AuthorizationCanceledEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
