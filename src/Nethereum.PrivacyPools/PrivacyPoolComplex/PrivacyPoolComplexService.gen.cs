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
using Nethereum.PrivacyPools.PrivacyPoolComplex.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Withdrawal = Nethereum.PrivacyPools.PrivacyPoolBase.Withdrawal;

namespace Nethereum.PrivacyPools.PrivacyPoolComplex
{
    public partial class PrivacyPoolComplexService: PrivacyPoolComplexServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, PrivacyPoolComplexDeployment privacyPoolComplexDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PrivacyPoolComplexDeployment>().SendRequestAndWaitForReceiptAsync(privacyPoolComplexDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, PrivacyPoolComplexDeployment privacyPoolComplexDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PrivacyPoolComplexDeployment>().SendRequestAsync(privacyPoolComplexDeployment);
        }

        public static async Task<PrivacyPoolComplexService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, PrivacyPoolComplexDeployment privacyPoolComplexDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, privacyPoolComplexDeployment, cancellationTokenSource);
            return new PrivacyPoolComplexService(web3, receipt.ContractAddress);
        }

        public PrivacyPoolComplexService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PrivacyPoolComplexServiceBase: ContractWeb3ServiceBase
    {

        public PrivacyPoolComplexServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> AssetQueryAsync(AssetFunction assetFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AssetFunction, string>(assetFunction, blockParameter);
        }

        
        public virtual Task<string> AssetQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AssetFunction, string>(null, blockParameter);
        }

        public Task<string> EntrypointQueryAsync(EntrypointFunction entrypointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntrypointFunction, string>(entrypointFunction, blockParameter);
        }

        
        public virtual Task<string> EntrypointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntrypointFunction, string>(null, blockParameter);
        }

        public Task<uint> MaxTreeDepthQueryAsync(MaxTreeDepthFunction maxTreeDepthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxTreeDepthFunction, uint>(maxTreeDepthFunction, blockParameter);
        }

        
        public virtual Task<uint> MaxTreeDepthQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxTreeDepthFunction, uint>(null, blockParameter);
        }

        public Task<string> RagequitVerifierQueryAsync(RagequitVerifierFunction ragequitVerifierFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RagequitVerifierFunction, string>(ragequitVerifierFunction, blockParameter);
        }

        
        public virtual Task<string> RagequitVerifierQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RagequitVerifierFunction, string>(null, blockParameter);
        }

        public Task<uint> RootHistorySizeQueryAsync(RootHistorySizeFunction rootHistorySizeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootHistorySizeFunction, uint>(rootHistorySizeFunction, blockParameter);
        }

        
        public virtual Task<uint> RootHistorySizeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootHistorySizeFunction, uint>(null, blockParameter);
        }

        public Task<BigInteger> ScopeQueryAsync(ScopeFunction scopeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ScopeFunction, BigInteger>(scopeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ScopeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ScopeFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> WithdrawalVerifierQueryAsync(WithdrawalVerifierFunction withdrawalVerifierFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WithdrawalVerifierFunction, string>(withdrawalVerifierFunction, blockParameter);
        }

        
        public virtual Task<string> WithdrawalVerifierQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WithdrawalVerifierFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> CurrentRootQueryAsync(CurrentRootFunction currentRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentRootFunction, BigInteger>(currentRootFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CurrentRootQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentRootFunction, BigInteger>(null, blockParameter);
        }

        public Task<uint> CurrentRootIndexQueryAsync(CurrentRootIndexFunction currentRootIndexFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentRootIndexFunction, uint>(currentRootIndexFunction, blockParameter);
        }

        
        public virtual Task<uint> CurrentRootIndexQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentRootIndexFunction, uint>(null, blockParameter);
        }

        public Task<BigInteger> CurrentTreeDepthQueryAsync(CurrentTreeDepthFunction currentTreeDepthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentTreeDepthFunction, BigInteger>(currentTreeDepthFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CurrentTreeDepthQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentTreeDepthFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> CurrentTreeSizeQueryAsync(CurrentTreeSizeFunction currentTreeSizeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentTreeSizeFunction, BigInteger>(currentTreeSizeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CurrentTreeSizeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrentTreeSizeFunction, BigInteger>(null, blockParameter);
        }

        public Task<bool> DeadQueryAsync(DeadFunction deadFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DeadFunction, bool>(deadFunction, blockParameter);
        }

        
        public virtual Task<bool> DeadQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DeadFunction, bool>(null, blockParameter);
        }

        public virtual Task<string> DepositRequestAsync(DepositFunction depositFunction)
        {
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(DepositFunction depositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public virtual Task<string> DepositRequestAsync(string depositor, BigInteger value, BigInteger precommitmentHash)
        {
            var depositFunction = new DepositFunction();
                depositFunction.Depositor = depositor;
                depositFunction.Value = value;
                depositFunction.PrecommitmentHash = precommitmentHash;
            
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(string depositor, BigInteger value, BigInteger precommitmentHash, CancellationTokenSource cancellationToken = null)
        {
            var depositFunction = new DepositFunction();
                depositFunction.Depositor = depositor;
                depositFunction.Value = value;
                depositFunction.PrecommitmentHash = precommitmentHash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public Task<string> DepositorsQueryAsync(DepositorsFunction depositorsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DepositorsFunction, string>(depositorsFunction, blockParameter);
        }

        
        public virtual Task<string> DepositorsQueryAsync(BigInteger label, BlockParameter blockParameter = null)
        {
            var depositorsFunction = new DepositorsFunction();
                depositorsFunction.Label = label;
            
            return ContractHandler.QueryAsync<DepositorsFunction, string>(depositorsFunction, blockParameter);
        }

        public Task<BigInteger> NonceQueryAsync(NonceFunction nonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(nonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(null, blockParameter);
        }

        public Task<bool> NullifierHashesQueryAsync(NullifierHashesFunction nullifierHashesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NullifierHashesFunction, bool>(nullifierHashesFunction, blockParameter);
        }

        
        public virtual Task<bool> NullifierHashesQueryAsync(BigInteger nullifierHash, BlockParameter blockParameter = null)
        {
            var nullifierHashesFunction = new NullifierHashesFunction();
                nullifierHashesFunction.NullifierHash = nullifierHash;
            
            return ContractHandler.QueryAsync<NullifierHashesFunction, bool>(nullifierHashesFunction, blockParameter);
        }

        public virtual Task<string> RagequitRequestAsync(RagequitFunction ragequitFunction)
        {
             return ContractHandler.SendRequestAsync(ragequitFunction);
        }

        public virtual Task<TransactionReceipt> RagequitRequestAndWaitForReceiptAsync(RagequitFunction ragequitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(ragequitFunction, cancellationToken);
        }

        public virtual Task<string> RagequitRequestAsync(RagequitProof proof)
        {
            var ragequitFunction = new RagequitFunction();
                ragequitFunction.Proof = proof;
            
             return ContractHandler.SendRequestAsync(ragequitFunction);
        }

        public virtual Task<TransactionReceipt> RagequitRequestAndWaitForReceiptAsync(RagequitProof proof, CancellationTokenSource cancellationToken = null)
        {
            var ragequitFunction = new RagequitFunction();
                ragequitFunction.Proof = proof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(ragequitFunction, cancellationToken);
        }

        public Task<BigInteger> RootsQueryAsync(RootsFunction rootsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootsFunction, BigInteger>(rootsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> RootsQueryAsync(BigInteger index, BlockParameter blockParameter = null)
        {
            var rootsFunction = new RootsFunction();
                rootsFunction.Index = index;
            
            return ContractHandler.QueryAsync<RootsFunction, BigInteger>(rootsFunction, blockParameter);
        }

        public virtual Task<string> WindDownRequestAsync(WindDownFunction windDownFunction)
        {
             return ContractHandler.SendRequestAsync(windDownFunction);
        }

        public virtual Task<string> WindDownRequestAsync()
        {
             return ContractHandler.SendRequestAsync<WindDownFunction>();
        }

        public virtual Task<TransactionReceipt> WindDownRequestAndWaitForReceiptAsync(WindDownFunction windDownFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(windDownFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> WindDownRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<WindDownFunction>(null, cancellationToken);
        }

        public virtual Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawRequestAsync(Withdrawal withdrawal, WithdrawProof proof)
        {
            var withdrawFunction = new WithdrawFunction();
                withdrawFunction.Withdrawal = withdrawal;
                withdrawFunction.Proof = proof;
            
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(Withdrawal withdrawal, WithdrawProof proof, CancellationTokenSource cancellationToken = null)
        {
            var withdrawFunction = new WithdrawFunction();
                withdrawFunction.Withdrawal = withdrawal;
                withdrawFunction.Proof = proof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AssetFunction),
                typeof(EntrypointFunction),
                typeof(MaxTreeDepthFunction),
                typeof(RagequitVerifierFunction),
                typeof(RootHistorySizeFunction),
                typeof(ScopeFunction),
                typeof(WithdrawalVerifierFunction),
                typeof(CurrentRootFunction),
                typeof(CurrentRootIndexFunction),
                typeof(CurrentTreeDepthFunction),
                typeof(CurrentTreeSizeFunction),
                typeof(DeadFunction),
                typeof(DepositFunction),
                typeof(DepositorsFunction),
                typeof(NonceFunction),
                typeof(NullifierHashesFunction),
                typeof(RagequitFunction),
                typeof(RootsFunction),
                typeof(WindDownFunction),
                typeof(WithdrawFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(DepositedEventDTO),
                typeof(LeafInsertedEventDTO),
                typeof(PoolDiedEventDTO),
                typeof(RagequitEventDTO),
                typeof(WithdrawnEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(ContextMismatchError),
                typeof(IncorrectASPRootError),
                typeof(InvalidCommitmentError),
                typeof(InvalidDepositValueError),
                typeof(InvalidProcessooorError),
                typeof(InvalidProofError),
                typeof(InvalidTreeDepthError),
                typeof(LeafAlreadyExistsError),
                typeof(LeafCannotBeZeroError),
                typeof(LeafGreaterThanSnarkScalarFieldError),
                typeof(MaxTreeDepthReachedError),
                typeof(NativeAssetNotAcceptedError),
                typeof(NativeAssetNotSupportedError),
                typeof(NotYetRagequitteableError),
                typeof(NullifierAlreadySpentError),
                typeof(OnlyEntrypointError),
                typeof(OnlyOriginalDepositorError),
                typeof(PoolIsDeadError),
                typeof(SafeERC20FailedOperationError),
                typeof(ScopeMismatchError),
                typeof(UnknownStateRootError),
                typeof(ZeroAddressError)
            };
        }
    }
}
