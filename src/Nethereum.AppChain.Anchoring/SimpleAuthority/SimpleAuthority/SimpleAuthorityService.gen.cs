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
using Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.SimpleAuthority
{
    public partial class SimpleAuthorityService: SimpleAuthorityServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SimpleAuthorityDeployment simpleAuthorityDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SimpleAuthorityDeployment>().SendRequestAndWaitForReceiptAsync(simpleAuthorityDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SimpleAuthorityDeployment simpleAuthorityDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SimpleAuthorityDeployment>().SendRequestAsync(simpleAuthorityDeployment);
        }

        public static async Task<SimpleAuthorityService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SimpleAuthorityDeployment simpleAuthorityDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, simpleAuthorityDeployment, cancellationTokenSource);
            return new SimpleAuthorityService(web3, receipt.ContractAddress);
        }

        public SimpleAuthorityService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SimpleAuthorityServiceBase: ContractWeb3ServiceBase
    {

        public SimpleAuthorityServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AuthorizeProverRequestAsync(AuthorizeProverFunction authorizeProverFunction)
        {
             return ContractHandler.SendRequestAsync(authorizeProverFunction);
        }

        public virtual Task<TransactionReceipt> AuthorizeProverRequestAndWaitForReceiptAsync(AuthorizeProverFunction authorizeProverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(authorizeProverFunction, cancellationToken);
        }

        public virtual Task<string> AuthorizeProverRequestAsync(ulong chainId, string prover)
        {
            var authorizeProverFunction = new AuthorizeProverFunction();
                authorizeProverFunction.ChainId = chainId;
                authorizeProverFunction.Prover = prover;
            
             return ContractHandler.SendRequestAsync(authorizeProverFunction);
        }

        public virtual Task<TransactionReceipt> AuthorizeProverRequestAndWaitForReceiptAsync(ulong chainId, string prover, CancellationTokenSource cancellationToken = null)
        {
            var authorizeProverFunction = new AuthorizeProverFunction();
                authorizeProverFunction.ChainId = chainId;
                authorizeProverFunction.Prover = prover;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(authorizeProverFunction, cancellationToken);
        }

        public Task<bool> AuthorizedProversQueryAsync(AuthorizedProversFunction authorizedProversFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AuthorizedProversFunction, bool>(authorizedProversFunction, blockParameter);
        }

        
        public virtual Task<bool> AuthorizedProversQueryAsync(ulong returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var authorizedProversFunction = new AuthorizedProversFunction();
                authorizedProversFunction.ReturnValue1 = returnValue1;
                authorizedProversFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<AuthorizedProversFunction, bool>(authorizedProversFunction, blockParameter);
        }

        public Task<bool> CanManageChainQueryAsync(CanManageChainFunction canManageChainFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CanManageChainFunction, bool>(canManageChainFunction, blockParameter);
        }

        
        public virtual Task<bool> CanManageChainQueryAsync(ulong chainId, string caller, BlockParameter blockParameter = null)
        {
            var canManageChainFunction = new CanManageChainFunction();
                canManageChainFunction.ChainId = chainId;
                canManageChainFunction.Caller = caller;
            
            return ContractHandler.QueryAsync<CanManageChainFunction, bool>(canManageChainFunction, blockParameter);
        }

        public Task<bool> CanProveQueryAsync(CanProveFunction canProveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CanProveFunction, bool>(canProveFunction, blockParameter);
        }

        
        public virtual Task<bool> CanProveQueryAsync(ulong chainId, string caller, BlockParameter blockParameter = null)
        {
            var canProveFunction = new CanProveFunction();
                canProveFunction.ChainId = chainId;
                canProveFunction.Caller = caller;
            
            return ContractHandler.QueryAsync<CanProveFunction, bool>(canProveFunction, blockParameter);
        }

        public Task<bool> CanSubmitAnchorQueryAsync(CanSubmitAnchorFunction canSubmitAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CanSubmitAnchorFunction, bool>(canSubmitAnchorFunction, blockParameter);
        }

        
        public virtual Task<bool> CanSubmitAnchorQueryAsync(ulong chainId, string caller, BlockParameter blockParameter = null)
        {
            var canSubmitAnchorFunction = new CanSubmitAnchorFunction();
                canSubmitAnchorFunction.ChainId = chainId;
                canSubmitAnchorFunction.Caller = caller;
            
            return ContractHandler.QueryAsync<CanSubmitAnchorFunction, bool>(canSubmitAnchorFunction, blockParameter);
        }

        public Task<string> OperatorsQueryAsync(OperatorsFunction operatorsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OperatorsFunction, string>(operatorsFunction, blockParameter);
        }

        
        public virtual Task<string> OperatorsQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var operatorsFunction = new OperatorsFunction();
                operatorsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<OperatorsFunction, string>(operatorsFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> RevokeProverRequestAsync(RevokeProverFunction revokeProverFunction)
        {
             return ContractHandler.SendRequestAsync(revokeProverFunction);
        }

        public virtual Task<TransactionReceipt> RevokeProverRequestAndWaitForReceiptAsync(RevokeProverFunction revokeProverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeProverFunction, cancellationToken);
        }

        public virtual Task<string> RevokeProverRequestAsync(ulong chainId, string prover)
        {
            var revokeProverFunction = new RevokeProverFunction();
                revokeProverFunction.ChainId = chainId;
                revokeProverFunction.Prover = prover;
            
             return ContractHandler.SendRequestAsync(revokeProverFunction);
        }

        public virtual Task<TransactionReceipt> RevokeProverRequestAndWaitForReceiptAsync(ulong chainId, string prover, CancellationTokenSource cancellationToken = null)
        {
            var revokeProverFunction = new RevokeProverFunction();
                revokeProverFunction.ChainId = chainId;
                revokeProverFunction.Prover = prover;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeProverFunction, cancellationToken);
        }

        public virtual Task<string> SetOperatorRequestAsync(SetOperatorFunction setOperatorFunction)
        {
             return ContractHandler.SendRequestAsync(setOperatorFunction);
        }

        public virtual Task<TransactionReceipt> SetOperatorRequestAndWaitForReceiptAsync(SetOperatorFunction setOperatorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOperatorFunction, cancellationToken);
        }

        public virtual Task<string> SetOperatorRequestAsync(ulong chainId, string newOperator)
        {
            var setOperatorFunction = new SetOperatorFunction();
                setOperatorFunction.ChainId = chainId;
                setOperatorFunction.NewOperator = newOperator;
            
             return ContractHandler.SendRequestAsync(setOperatorFunction);
        }

        public virtual Task<TransactionReceipt> SetOperatorRequestAndWaitForReceiptAsync(ulong chainId, string newOperator, CancellationTokenSource cancellationToken = null)
        {
            var setOperatorFunction = new SetOperatorFunction();
                setOperatorFunction.ChainId = chainId;
                setOperatorFunction.NewOperator = newOperator;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOperatorFunction, cancellationToken);
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AuthorizeProverFunction),
                typeof(AuthorizedProversFunction),
                typeof(CanManageChainFunction),
                typeof(CanProveFunction),
                typeof(CanSubmitAnchorFunction),
                typeof(OperatorsFunction),
                typeof(OwnerFunction),
                typeof(RevokeProverFunction),
                typeof(SetOperatorFunction),
                typeof(TransferOwnershipFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(OperatorSetEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(ProverAuthorizedEventDTO),
                typeof(ProverRevokedEventDTO)
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
