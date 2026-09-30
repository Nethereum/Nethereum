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
using Nethereum.DID.EthrDID.EthereumDIDRegistry.ContractDefinition;

namespace Nethereum.DID.EthrDID.EthereumDIDRegistry
{
    public partial class EthereumDIDRegistryService: EthereumDIDRegistryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, EthereumDIDRegistryDeployment ethereumDIDRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EthereumDIDRegistryDeployment>().SendRequestAndWaitForReceiptAsync(ethereumDIDRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, EthereumDIDRegistryDeployment ethereumDIDRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EthereumDIDRegistryDeployment>().SendRequestAsync(ethereumDIDRegistryDeployment);
        }

        public static async Task<EthereumDIDRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, EthereumDIDRegistryDeployment ethereumDIDRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ethereumDIDRegistryDeployment, cancellationTokenSource);
            return new EthereumDIDRegistryService(web3, receipt.ContractAddress);
        }

        public EthereumDIDRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class EthereumDIDRegistryServiceBase: ContractWeb3ServiceBase
    {

        public EthereumDIDRegistryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddDelegateRequestAsync(AddDelegate1Function addDelegate1Function)
        {
             return ContractHandler.SendRequestAsync(addDelegate1Function);
        }

        public virtual Task<TransactionReceipt> AddDelegateRequestAndWaitForReceiptAsync(AddDelegate1Function addDelegate1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegate1Function, cancellationToken);
        }

        public virtual Task<string> AddDelegateRequestAsync(string identity, string actor, byte[] delegateType, string @delegate, BigInteger validity)
        {
            var addDelegate1Function = new AddDelegate1Function();
                addDelegate1Function.Identity = identity;
                addDelegate1Function.Actor = actor;
                addDelegate1Function.DelegateType = delegateType;
                addDelegate1Function.Delegate = @delegate;
                addDelegate1Function.Validity = validity;
            
             return ContractHandler.SendRequestAsync(addDelegate1Function);
        }

        public virtual Task<TransactionReceipt> AddDelegateRequestAndWaitForReceiptAsync(string identity, string actor, byte[] delegateType, string @delegate, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var addDelegate1Function = new AddDelegate1Function();
                addDelegate1Function.Identity = identity;
                addDelegate1Function.Actor = actor;
                addDelegate1Function.DelegateType = delegateType;
                addDelegate1Function.Delegate = @delegate;
                addDelegate1Function.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegate1Function, cancellationToken);
        }

        public virtual Task<string> AddDelegateRequestAsync(AddDelegateFunction addDelegateFunction)
        {
             return ContractHandler.SendRequestAsync(addDelegateFunction);
        }

        public virtual Task<TransactionReceipt> AddDelegateRequestAndWaitForReceiptAsync(AddDelegateFunction addDelegateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegateFunction, cancellationToken);
        }

        public virtual Task<string> AddDelegateRequestAsync(string identity, byte[] delegateType, string @delegate, BigInteger validity)
        {
            var addDelegateFunction = new AddDelegateFunction();
                addDelegateFunction.Identity = identity;
                addDelegateFunction.DelegateType = delegateType;
                addDelegateFunction.Delegate = @delegate;
                addDelegateFunction.Validity = validity;
            
             return ContractHandler.SendRequestAsync(addDelegateFunction);
        }

        public virtual Task<TransactionReceipt> AddDelegateRequestAndWaitForReceiptAsync(string identity, byte[] delegateType, string @delegate, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var addDelegateFunction = new AddDelegateFunction();
                addDelegateFunction.Identity = identity;
                addDelegateFunction.DelegateType = delegateType;
                addDelegateFunction.Delegate = @delegate;
                addDelegateFunction.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegateFunction, cancellationToken);
        }

        public virtual Task<string> AddDelegateSignedRequestAsync(AddDelegateSignedFunction addDelegateSignedFunction)
        {
             return ContractHandler.SendRequestAsync(addDelegateSignedFunction);
        }

        public virtual Task<TransactionReceipt> AddDelegateSignedRequestAndWaitForReceiptAsync(AddDelegateSignedFunction addDelegateSignedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegateSignedFunction, cancellationToken);
        }

        public virtual Task<string> AddDelegateSignedRequestAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] delegateType, string @delegate, BigInteger validity)
        {
            var addDelegateSignedFunction = new AddDelegateSignedFunction();
                addDelegateSignedFunction.Identity = identity;
                addDelegateSignedFunction.SigV = sigV;
                addDelegateSignedFunction.SigR = sigR;
                addDelegateSignedFunction.SigS = sigS;
                addDelegateSignedFunction.DelegateType = delegateType;
                addDelegateSignedFunction.Delegate = @delegate;
                addDelegateSignedFunction.Validity = validity;
            
             return ContractHandler.SendRequestAsync(addDelegateSignedFunction);
        }

        public virtual Task<TransactionReceipt> AddDelegateSignedRequestAndWaitForReceiptAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] delegateType, string @delegate, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var addDelegateSignedFunction = new AddDelegateSignedFunction();
                addDelegateSignedFunction.Identity = identity;
                addDelegateSignedFunction.SigV = sigV;
                addDelegateSignedFunction.SigR = sigR;
                addDelegateSignedFunction.SigS = sigS;
                addDelegateSignedFunction.DelegateType = delegateType;
                addDelegateSignedFunction.Delegate = @delegate;
                addDelegateSignedFunction.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDelegateSignedFunction, cancellationToken);
        }

        public virtual Task<string> ChangeOwnerRequestAsync(ChangeOwnerFunction changeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(changeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerRequestAndWaitForReceiptAsync(ChangeOwnerFunction changeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerFunction, cancellationToken);
        }

        public virtual Task<string> ChangeOwnerRequestAsync(string identity, string actor, string newOwner)
        {
            var changeOwnerFunction = new ChangeOwnerFunction();
                changeOwnerFunction.Identity = identity;
                changeOwnerFunction.Actor = actor;
                changeOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(changeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerRequestAndWaitForReceiptAsync(string identity, string actor, string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var changeOwnerFunction = new ChangeOwnerFunction();
                changeOwnerFunction.Identity = identity;
                changeOwnerFunction.Actor = actor;
                changeOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerFunction, cancellationToken);
        }

        public virtual Task<string> ChangeOwnerSignedRequestAsync(ChangeOwnerSignedFunction changeOwnerSignedFunction)
        {
             return ContractHandler.SendRequestAsync(changeOwnerSignedFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerSignedRequestAndWaitForReceiptAsync(ChangeOwnerSignedFunction changeOwnerSignedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerSignedFunction, cancellationToken);
        }

        public virtual Task<string> ChangeOwnerSignedRequestAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, string newOwner)
        {
            var changeOwnerSignedFunction = new ChangeOwnerSignedFunction();
                changeOwnerSignedFunction.Identity = identity;
                changeOwnerSignedFunction.SigV = sigV;
                changeOwnerSignedFunction.SigR = sigR;
                changeOwnerSignedFunction.SigS = sigS;
                changeOwnerSignedFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(changeOwnerSignedFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerSignedRequestAndWaitForReceiptAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var changeOwnerSignedFunction = new ChangeOwnerSignedFunction();
                changeOwnerSignedFunction.Identity = identity;
                changeOwnerSignedFunction.SigV = sigV;
                changeOwnerSignedFunction.SigR = sigR;
                changeOwnerSignedFunction.SigS = sigS;
                changeOwnerSignedFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerSignedFunction, cancellationToken);
        }

        public Task<BigInteger> ChangedQueryAsync(ChangedFunction changedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ChangedFunction, BigInteger>(changedFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ChangedQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var changedFunction = new ChangedFunction();
                changedFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ChangedFunction, BigInteger>(changedFunction, blockParameter);
        }

        public Task<BigInteger> DelegatesQueryAsync(DelegatesFunction delegatesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DelegatesFunction, BigInteger>(delegatesFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DelegatesQueryAsync(string returnValue1, byte[] returnValue2, string returnValue3, BlockParameter blockParameter = null)
        {
            var delegatesFunction = new DelegatesFunction();
                delegatesFunction.ReturnValue1 = returnValue1;
                delegatesFunction.ReturnValue2 = returnValue2;
                delegatesFunction.ReturnValue3 = returnValue3;
            
            return ContractHandler.QueryAsync<DelegatesFunction, BigInteger>(delegatesFunction, blockParameter);
        }

        public Task<string> IdentityOwnerQueryAsync(IdentityOwnerFunction identityOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IdentityOwnerFunction, string>(identityOwnerFunction, blockParameter);
        }

        
        public virtual Task<string> IdentityOwnerQueryAsync(string identity, BlockParameter blockParameter = null)
        {
            var identityOwnerFunction = new IdentityOwnerFunction();
                identityOwnerFunction.Identity = identity;
            
            return ContractHandler.QueryAsync<IdentityOwnerFunction, string>(identityOwnerFunction, blockParameter);
        }

        public Task<BigInteger> NonceQueryAsync(NonceFunction nonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(nonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var nonceFunction = new NonceFunction();
                nonceFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(nonceFunction, blockParameter);
        }

        public Task<string> OwnersQueryAsync(OwnersFunction ownersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnersFunction, string>(ownersFunction, blockParameter);
        }

        
        public virtual Task<string> OwnersQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var ownersFunction = new OwnersFunction();
                ownersFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<OwnersFunction, string>(ownersFunction, blockParameter);
        }

        public virtual Task<string> RevokeAttributeRequestAsync(RevokeAttributeFunction revokeAttributeFunction)
        {
             return ContractHandler.SendRequestAsync(revokeAttributeFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeRequestAndWaitForReceiptAsync(RevokeAttributeFunction revokeAttributeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttributeFunction, cancellationToken);
        }

        public virtual Task<string> RevokeAttributeRequestAsync(string identity, byte[] name, byte[] value)
        {
            var revokeAttributeFunction = new RevokeAttributeFunction();
                revokeAttributeFunction.Identity = identity;
                revokeAttributeFunction.Name = name;
                revokeAttributeFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(revokeAttributeFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeRequestAndWaitForReceiptAsync(string identity, byte[] name, byte[] value, CancellationTokenSource cancellationToken = null)
        {
            var revokeAttributeFunction = new RevokeAttributeFunction();
                revokeAttributeFunction.Identity = identity;
                revokeAttributeFunction.Name = name;
                revokeAttributeFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttributeFunction, cancellationToken);
        }

        public virtual Task<string> RevokeAttributeRequestAsync(RevokeAttribute1Function revokeAttribute1Function)
        {
             return ContractHandler.SendRequestAsync(revokeAttribute1Function);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeRequestAndWaitForReceiptAsync(RevokeAttribute1Function revokeAttribute1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttribute1Function, cancellationToken);
        }

        public virtual Task<string> RevokeAttributeRequestAsync(string identity, string actor, byte[] name, byte[] value)
        {
            var revokeAttribute1Function = new RevokeAttribute1Function();
                revokeAttribute1Function.Identity = identity;
                revokeAttribute1Function.Actor = actor;
                revokeAttribute1Function.Name = name;
                revokeAttribute1Function.Value = value;
            
             return ContractHandler.SendRequestAsync(revokeAttribute1Function);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeRequestAndWaitForReceiptAsync(string identity, string actor, byte[] name, byte[] value, CancellationTokenSource cancellationToken = null)
        {
            var revokeAttribute1Function = new RevokeAttribute1Function();
                revokeAttribute1Function.Identity = identity;
                revokeAttribute1Function.Actor = actor;
                revokeAttribute1Function.Name = name;
                revokeAttribute1Function.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttribute1Function, cancellationToken);
        }

        public virtual Task<string> RevokeAttributeSignedRequestAsync(RevokeAttributeSignedFunction revokeAttributeSignedFunction)
        {
             return ContractHandler.SendRequestAsync(revokeAttributeSignedFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeSignedRequestAndWaitForReceiptAsync(RevokeAttributeSignedFunction revokeAttributeSignedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttributeSignedFunction, cancellationToken);
        }

        public virtual Task<string> RevokeAttributeSignedRequestAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] name, byte[] value)
        {
            var revokeAttributeSignedFunction = new RevokeAttributeSignedFunction();
                revokeAttributeSignedFunction.Identity = identity;
                revokeAttributeSignedFunction.SigV = sigV;
                revokeAttributeSignedFunction.SigR = sigR;
                revokeAttributeSignedFunction.SigS = sigS;
                revokeAttributeSignedFunction.Name = name;
                revokeAttributeSignedFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(revokeAttributeSignedFunction);
        }

        public virtual Task<TransactionReceipt> RevokeAttributeSignedRequestAndWaitForReceiptAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] name, byte[] value, CancellationTokenSource cancellationToken = null)
        {
            var revokeAttributeSignedFunction = new RevokeAttributeSignedFunction();
                revokeAttributeSignedFunction.Identity = identity;
                revokeAttributeSignedFunction.SigV = sigV;
                revokeAttributeSignedFunction.SigR = sigR;
                revokeAttributeSignedFunction.SigS = sigS;
                revokeAttributeSignedFunction.Name = name;
                revokeAttributeSignedFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeAttributeSignedFunction, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateRequestAsync(RevokeDelegateFunction revokeDelegateFunction)
        {
             return ContractHandler.SendRequestAsync(revokeDelegateFunction);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateRequestAndWaitForReceiptAsync(RevokeDelegateFunction revokeDelegateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegateFunction, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateRequestAsync(string identity, byte[] delegateType, string @delegate)
        {
            var revokeDelegateFunction = new RevokeDelegateFunction();
                revokeDelegateFunction.Identity = identity;
                revokeDelegateFunction.DelegateType = delegateType;
                revokeDelegateFunction.Delegate = @delegate;
            
             return ContractHandler.SendRequestAsync(revokeDelegateFunction);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateRequestAndWaitForReceiptAsync(string identity, byte[] delegateType, string @delegate, CancellationTokenSource cancellationToken = null)
        {
            var revokeDelegateFunction = new RevokeDelegateFunction();
                revokeDelegateFunction.Identity = identity;
                revokeDelegateFunction.DelegateType = delegateType;
                revokeDelegateFunction.Delegate = @delegate;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegateFunction, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateRequestAsync(RevokeDelegate1Function revokeDelegate1Function)
        {
             return ContractHandler.SendRequestAsync(revokeDelegate1Function);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateRequestAndWaitForReceiptAsync(RevokeDelegate1Function revokeDelegate1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegate1Function, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateRequestAsync(string identity, string actor, byte[] delegateType, string @delegate)
        {
            var revokeDelegate1Function = new RevokeDelegate1Function();
                revokeDelegate1Function.Identity = identity;
                revokeDelegate1Function.Actor = actor;
                revokeDelegate1Function.DelegateType = delegateType;
                revokeDelegate1Function.Delegate = @delegate;
            
             return ContractHandler.SendRequestAsync(revokeDelegate1Function);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateRequestAndWaitForReceiptAsync(string identity, string actor, byte[] delegateType, string @delegate, CancellationTokenSource cancellationToken = null)
        {
            var revokeDelegate1Function = new RevokeDelegate1Function();
                revokeDelegate1Function.Identity = identity;
                revokeDelegate1Function.Actor = actor;
                revokeDelegate1Function.DelegateType = delegateType;
                revokeDelegate1Function.Delegate = @delegate;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegate1Function, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateSignedRequestAsync(RevokeDelegateSignedFunction revokeDelegateSignedFunction)
        {
             return ContractHandler.SendRequestAsync(revokeDelegateSignedFunction);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateSignedRequestAndWaitForReceiptAsync(RevokeDelegateSignedFunction revokeDelegateSignedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegateSignedFunction, cancellationToken);
        }

        public virtual Task<string> RevokeDelegateSignedRequestAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] delegateType, string @delegate)
        {
            var revokeDelegateSignedFunction = new RevokeDelegateSignedFunction();
                revokeDelegateSignedFunction.Identity = identity;
                revokeDelegateSignedFunction.SigV = sigV;
                revokeDelegateSignedFunction.SigR = sigR;
                revokeDelegateSignedFunction.SigS = sigS;
                revokeDelegateSignedFunction.DelegateType = delegateType;
                revokeDelegateSignedFunction.Delegate = @delegate;
            
             return ContractHandler.SendRequestAsync(revokeDelegateSignedFunction);
        }

        public virtual Task<TransactionReceipt> RevokeDelegateSignedRequestAndWaitForReceiptAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] delegateType, string @delegate, CancellationTokenSource cancellationToken = null)
        {
            var revokeDelegateSignedFunction = new RevokeDelegateSignedFunction();
                revokeDelegateSignedFunction.Identity = identity;
                revokeDelegateSignedFunction.SigV = sigV;
                revokeDelegateSignedFunction.SigR = sigR;
                revokeDelegateSignedFunction.SigS = sigS;
                revokeDelegateSignedFunction.DelegateType = delegateType;
                revokeDelegateSignedFunction.Delegate = @delegate;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeDelegateSignedFunction, cancellationToken);
        }

        public virtual Task<string> SetAttributeRequestAsync(SetAttributeFunction setAttributeFunction)
        {
             return ContractHandler.SendRequestAsync(setAttributeFunction);
        }

        public virtual Task<TransactionReceipt> SetAttributeRequestAndWaitForReceiptAsync(SetAttributeFunction setAttributeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttributeFunction, cancellationToken);
        }

        public virtual Task<string> SetAttributeRequestAsync(string identity, byte[] name, byte[] value, BigInteger validity)
        {
            var setAttributeFunction = new SetAttributeFunction();
                setAttributeFunction.Identity = identity;
                setAttributeFunction.Name = name;
                setAttributeFunction.Value = value;
                setAttributeFunction.Validity = validity;
            
             return ContractHandler.SendRequestAsync(setAttributeFunction);
        }

        public virtual Task<TransactionReceipt> SetAttributeRequestAndWaitForReceiptAsync(string identity, byte[] name, byte[] value, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var setAttributeFunction = new SetAttributeFunction();
                setAttributeFunction.Identity = identity;
                setAttributeFunction.Name = name;
                setAttributeFunction.Value = value;
                setAttributeFunction.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttributeFunction, cancellationToken);
        }

        public virtual Task<string> SetAttributeRequestAsync(SetAttribute1Function setAttribute1Function)
        {
             return ContractHandler.SendRequestAsync(setAttribute1Function);
        }

        public virtual Task<TransactionReceipt> SetAttributeRequestAndWaitForReceiptAsync(SetAttribute1Function setAttribute1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttribute1Function, cancellationToken);
        }

        public virtual Task<string> SetAttributeRequestAsync(string identity, string actor, byte[] name, byte[] value, BigInteger validity)
        {
            var setAttribute1Function = new SetAttribute1Function();
                setAttribute1Function.Identity = identity;
                setAttribute1Function.Actor = actor;
                setAttribute1Function.Name = name;
                setAttribute1Function.Value = value;
                setAttribute1Function.Validity = validity;
            
             return ContractHandler.SendRequestAsync(setAttribute1Function);
        }

        public virtual Task<TransactionReceipt> SetAttributeRequestAndWaitForReceiptAsync(string identity, string actor, byte[] name, byte[] value, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var setAttribute1Function = new SetAttribute1Function();
                setAttribute1Function.Identity = identity;
                setAttribute1Function.Actor = actor;
                setAttribute1Function.Name = name;
                setAttribute1Function.Value = value;
                setAttribute1Function.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttribute1Function, cancellationToken);
        }

        public virtual Task<string> SetAttributeSignedRequestAsync(SetAttributeSignedFunction setAttributeSignedFunction)
        {
             return ContractHandler.SendRequestAsync(setAttributeSignedFunction);
        }

        public virtual Task<TransactionReceipt> SetAttributeSignedRequestAndWaitForReceiptAsync(SetAttributeSignedFunction setAttributeSignedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttributeSignedFunction, cancellationToken);
        }

        public virtual Task<string> SetAttributeSignedRequestAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] name, byte[] value, BigInteger validity)
        {
            var setAttributeSignedFunction = new SetAttributeSignedFunction();
                setAttributeSignedFunction.Identity = identity;
                setAttributeSignedFunction.SigV = sigV;
                setAttributeSignedFunction.SigR = sigR;
                setAttributeSignedFunction.SigS = sigS;
                setAttributeSignedFunction.Name = name;
                setAttributeSignedFunction.Value = value;
                setAttributeSignedFunction.Validity = validity;
            
             return ContractHandler.SendRequestAsync(setAttributeSignedFunction);
        }

        public virtual Task<TransactionReceipt> SetAttributeSignedRequestAndWaitForReceiptAsync(string identity, byte sigV, byte[] sigR, byte[] sigS, byte[] name, byte[] value, BigInteger validity, CancellationTokenSource cancellationToken = null)
        {
            var setAttributeSignedFunction = new SetAttributeSignedFunction();
                setAttributeSignedFunction.Identity = identity;
                setAttributeSignedFunction.SigV = sigV;
                setAttributeSignedFunction.SigR = sigR;
                setAttributeSignedFunction.SigS = sigS;
                setAttributeSignedFunction.Name = name;
                setAttributeSignedFunction.Value = value;
                setAttributeSignedFunction.Validity = validity;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAttributeSignedFunction, cancellationToken);
        }

        public Task<bool> ValidDelegateQueryAsync(ValidDelegateFunction validDelegateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidDelegateFunction, bool>(validDelegateFunction, blockParameter);
        }

        
        public virtual Task<bool> ValidDelegateQueryAsync(string identity, byte[] delegateType, string @delegate, BlockParameter blockParameter = null)
        {
            var validDelegateFunction = new ValidDelegateFunction();
                validDelegateFunction.Identity = identity;
                validDelegateFunction.DelegateType = delegateType;
                validDelegateFunction.Delegate = @delegate;
            
            return ContractHandler.QueryAsync<ValidDelegateFunction, bool>(validDelegateFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AddDelegate1Function),
                typeof(AddDelegateFunction),
                typeof(AddDelegateSignedFunction),
                typeof(ChangeOwnerFunction),
                typeof(ChangeOwnerSignedFunction),
                typeof(ChangedFunction),
                typeof(DelegatesFunction),
                typeof(IdentityOwnerFunction),
                typeof(NonceFunction),
                typeof(OwnersFunction),
                typeof(RevokeAttributeFunction),
                typeof(RevokeAttribute1Function),
                typeof(RevokeAttributeSignedFunction),
                typeof(RevokeDelegateFunction),
                typeof(RevokeDelegate1Function),
                typeof(RevokeDelegateSignedFunction),
                typeof(SetAttributeFunction),
                typeof(SetAttribute1Function),
                typeof(SetAttributeSignedFunction),
                typeof(ValidDelegateFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(DIDAttributeChangedEventDTO),
                typeof(DIDDelegateChangedEventDTO),
                typeof(DIDOwnerChangedEventDTO)
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
