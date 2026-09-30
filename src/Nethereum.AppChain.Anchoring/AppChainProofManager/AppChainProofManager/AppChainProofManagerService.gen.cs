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
using Nethereum.AppChain.Anchoring.AppChainProofManager.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.AppChainProofManager
{
    public partial class AppChainProofManagerService: AppChainProofManagerServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, AppChainProofManagerDeployment appChainProofManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainProofManagerDeployment>().SendRequestAndWaitForReceiptAsync(appChainProofManagerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, AppChainProofManagerDeployment appChainProofManagerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainProofManagerDeployment>().SendRequestAsync(appChainProofManagerDeployment);
        }

        public static async Task<AppChainProofManagerService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, AppChainProofManagerDeployment appChainProofManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, appChainProofManagerDeployment, cancellationTokenSource);
            return new AppChainProofManagerService(web3, receipt.ContractAddress);
        }

        public AppChainProofManagerService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class AppChainProofManagerServiceBase: ContractWeb3ServiceBase
    {

        public AppChainProofManagerServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> MaxProofBondQueryAsync(MaxProofBondFunction maxProofBondFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofBondFunction, BigInteger>(maxProofBondFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxProofBondQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofBondFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> MaxProofWindowQueryAsync(MaxProofWindowFunction maxProofWindowFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofWindowFunction, BigInteger>(maxProofWindowFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxProofWindowQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofWindowFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> MinProofWindowQueryAsync(MinProofWindowFunction minProofWindowFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinProofWindowFunction, BigInteger>(minProofWindowFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MinProofWindowQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinProofWindowFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> AnchorQueryAsync(AnchorFunction anchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AnchorFunction, string>(anchorFunction, blockParameter);
        }

        
        public virtual Task<string> AnchorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AnchorFunction, string>(null, blockParameter);
        }

        public virtual Task<BlockProofsOutputDTO> BlockProofsQueryAsync(BlockProofsFunction blockProofsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<BlockProofsFunction, BlockProofsOutputDTO>(blockProofsFunction, blockParameter);
        }

        public virtual Task<BlockProofsOutputDTO> BlockProofsQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var blockProofsFunction = new BlockProofsFunction();
                blockProofsFunction.ReturnValue1 = returnValue1;
                blockProofsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<BlockProofsFunction, BlockProofsOutputDTO>(blockProofsFunction, blockParameter);
        }

        public virtual Task<string> ClaimProofTimeoutRequestAsync(ClaimProofTimeoutFunction claimProofTimeoutFunction)
        {
             return ContractHandler.SendRequestAsync(claimProofTimeoutFunction);
        }

        public virtual Task<TransactionReceipt> ClaimProofTimeoutRequestAndWaitForReceiptAsync(ClaimProofTimeoutFunction claimProofTimeoutFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimProofTimeoutFunction, cancellationToken);
        }

        public virtual Task<string> ClaimProofTimeoutRequestAsync(ulong chainId, ulong blockNumber)
        {
            var claimProofTimeoutFunction = new ClaimProofTimeoutFunction();
                claimProofTimeoutFunction.ChainId = chainId;
                claimProofTimeoutFunction.BlockNumber = blockNumber;
            
             return ContractHandler.SendRequestAsync(claimProofTimeoutFunction);
        }

        public virtual Task<TransactionReceipt> ClaimProofTimeoutRequestAndWaitForReceiptAsync(ulong chainId, ulong blockNumber, CancellationTokenSource cancellationToken = null)
        {
            var claimProofTimeoutFunction = new ClaimProofTimeoutFunction();
                claimProofTimeoutFunction.ChainId = chainId;
                claimProofTimeoutFunction.BlockNumber = blockNumber;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimProofTimeoutFunction, cancellationToken);
        }

        public virtual Task<string> FulfillBlockProofRequestAsync(FulfillBlockProofFunction fulfillBlockProofFunction)
        {
             return ContractHandler.SendRequestAsync(fulfillBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> FulfillBlockProofRequestAndWaitForReceiptAsync(FulfillBlockProofFunction fulfillBlockProofFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(fulfillBlockProofFunction, cancellationToken);
        }

        public virtual Task<string> FulfillBlockProofRequestAsync(ulong chainId, ulong blockNumber, ulong anchorEndBlock, byte[] blockHash, byte[] preStateRoot, byte[] postStateRoot, List<byte[]> merkleProof, byte[] zkProof, byte proofSystem)
        {
            var fulfillBlockProofFunction = new FulfillBlockProofFunction();
                fulfillBlockProofFunction.ChainId = chainId;
                fulfillBlockProofFunction.BlockNumber = blockNumber;
                fulfillBlockProofFunction.AnchorEndBlock = anchorEndBlock;
                fulfillBlockProofFunction.BlockHash = blockHash;
                fulfillBlockProofFunction.PreStateRoot = preStateRoot;
                fulfillBlockProofFunction.PostStateRoot = postStateRoot;
                fulfillBlockProofFunction.MerkleProof = merkleProof;
                fulfillBlockProofFunction.ZkProof = zkProof;
                fulfillBlockProofFunction.ProofSystem = proofSystem;
            
             return ContractHandler.SendRequestAsync(fulfillBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> FulfillBlockProofRequestAndWaitForReceiptAsync(ulong chainId, ulong blockNumber, ulong anchorEndBlock, byte[] blockHash, byte[] preStateRoot, byte[] postStateRoot, List<byte[]> merkleProof, byte[] zkProof, byte proofSystem, CancellationTokenSource cancellationToken = null)
        {
            var fulfillBlockProofFunction = new FulfillBlockProofFunction();
                fulfillBlockProofFunction.ChainId = chainId;
                fulfillBlockProofFunction.BlockNumber = blockNumber;
                fulfillBlockProofFunction.AnchorEndBlock = anchorEndBlock;
                fulfillBlockProofFunction.BlockHash = blockHash;
                fulfillBlockProofFunction.PreStateRoot = preStateRoot;
                fulfillBlockProofFunction.PostStateRoot = postStateRoot;
                fulfillBlockProofFunction.MerkleProof = merkleProof;
                fulfillBlockProofFunction.ZkProof = zkProof;
                fulfillBlockProofFunction.ProofSystem = proofSystem;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(fulfillBlockProofFunction, cancellationToken);
        }

        public virtual Task<GetBlockProofOutputDTO> GetBlockProofQueryAsync(GetBlockProofFunction getBlockProofFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetBlockProofFunction, GetBlockProofOutputDTO>(getBlockProofFunction, blockParameter);
        }

        public virtual Task<GetBlockProofOutputDTO> GetBlockProofQueryAsync(ulong chainId, ulong blockNumber, BlockParameter blockParameter = null)
        {
            var getBlockProofFunction = new GetBlockProofFunction();
                getBlockProofFunction.ChainId = chainId;
                getBlockProofFunction.BlockNumber = blockNumber;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetBlockProofFunction, GetBlockProofOutputDTO>(getBlockProofFunction, blockParameter);
        }

        public Task<bool> IsBlockProvenQueryAsync(IsBlockProvenFunction isBlockProvenFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsBlockProvenFunction, bool>(isBlockProvenFunction, blockParameter);
        }

        
        public virtual Task<bool> IsBlockProvenQueryAsync(ulong chainId, ulong blockNumber, BlockParameter blockParameter = null)
        {
            var isBlockProvenFunction = new IsBlockProvenFunction();
                isBlockProvenFunction.ChainId = chainId;
                isBlockProvenFunction.BlockNumber = blockNumber;
            
            return ContractHandler.QueryAsync<IsBlockProvenFunction, bool>(isBlockProvenFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> PauseRequestAsync(PauseFunction pauseFunction)
        {
             return ContractHandler.SendRequestAsync(pauseFunction);
        }

        public virtual Task<string> PauseRequestAsync()
        {
             return ContractHandler.SendRequestAsync<PauseFunction>();
        }

        public virtual Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(PauseFunction pauseFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(pauseFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<PauseFunction>(null, cancellationToken);
        }

        public Task<bool> PausedQueryAsync(PausedFunction pausedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(pausedFunction, blockParameter);
        }

        
        public virtual Task<bool> PausedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(null, blockParameter);
        }

        public Task<BigInteger> PendingWithdrawalsQueryAsync(PendingWithdrawalsFunction pendingWithdrawalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PendingWithdrawalsFunction, BigInteger>(pendingWithdrawalsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> PendingWithdrawalsQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var pendingWithdrawalsFunction = new PendingWithdrawalsFunction();
                pendingWithdrawalsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<PendingWithdrawalsFunction, BigInteger>(pendingWithdrawalsFunction, blockParameter);
        }

        public Task<BigInteger> ProofBondQueryAsync(ProofBondFunction proofBondFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProofBondFunction, BigInteger>(proofBondFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ProofBondQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var proofBondFunction = new ProofBondFunction();
                proofBondFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ProofBondFunction, BigInteger>(proofBondFunction, blockParameter);
        }

        public virtual Task<ProofRequestsOutputDTO> ProofRequestsQueryAsync(ProofRequestsFunction proofRequestsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ProofRequestsFunction, ProofRequestsOutputDTO>(proofRequestsFunction, blockParameter);
        }

        public virtual Task<ProofRequestsOutputDTO> ProofRequestsQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var proofRequestsFunction = new ProofRequestsFunction();
                proofRequestsFunction.ReturnValue1 = returnValue1;
                proofRequestsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ProofRequestsFunction, ProofRequestsOutputDTO>(proofRequestsFunction, blockParameter);
        }

        public Task<BigInteger> ProofWindowQueryAsync(ProofWindowFunction proofWindowFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProofWindowFunction, BigInteger>(proofWindowFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ProofWindowQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var proofWindowFunction = new ProofWindowFunction();
                proofWindowFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ProofWindowFunction, BigInteger>(proofWindowFunction, blockParameter);
        }

        public virtual Task<string> RequestBlockProofRequestAsync(RequestBlockProofFunction requestBlockProofFunction)
        {
             return ContractHandler.SendRequestAsync(requestBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> RequestBlockProofRequestAndWaitForReceiptAsync(RequestBlockProofFunction requestBlockProofFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(requestBlockProofFunction, cancellationToken);
        }

        public virtual Task<string> RequestBlockProofRequestAsync(ulong chainId, ulong blockNumber)
        {
            var requestBlockProofFunction = new RequestBlockProofFunction();
                requestBlockProofFunction.ChainId = chainId;
                requestBlockProofFunction.BlockNumber = blockNumber;
            
             return ContractHandler.SendRequestAsync(requestBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> RequestBlockProofRequestAndWaitForReceiptAsync(ulong chainId, ulong blockNumber, CancellationTokenSource cancellationToken = null)
        {
            var requestBlockProofFunction = new RequestBlockProofFunction();
                requestBlockProofFunction.ChainId = chainId;
                requestBlockProofFunction.BlockNumber = blockNumber;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(requestBlockProofFunction, cancellationToken);
        }

        public virtual Task<string> SetProofBondRequestAsync(SetProofBondFunction setProofBondFunction)
        {
             return ContractHandler.SendRequestAsync(setProofBondFunction);
        }

        public virtual Task<TransactionReceipt> SetProofBondRequestAndWaitForReceiptAsync(SetProofBondFunction setProofBondFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProofBondFunction, cancellationToken);
        }

        public virtual Task<string> SetProofBondRequestAsync(ulong chainId, BigInteger newBond)
        {
            var setProofBondFunction = new SetProofBondFunction();
                setProofBondFunction.ChainId = chainId;
                setProofBondFunction.NewBond = newBond;
            
             return ContractHandler.SendRequestAsync(setProofBondFunction);
        }

        public virtual Task<TransactionReceipt> SetProofBondRequestAndWaitForReceiptAsync(ulong chainId, BigInteger newBond, CancellationTokenSource cancellationToken = null)
        {
            var setProofBondFunction = new SetProofBondFunction();
                setProofBondFunction.ChainId = chainId;
                setProofBondFunction.NewBond = newBond;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProofBondFunction, cancellationToken);
        }

        public virtual Task<string> SetProofWindowRequestAsync(SetProofWindowFunction setProofWindowFunction)
        {
             return ContractHandler.SendRequestAsync(setProofWindowFunction);
        }

        public virtual Task<TransactionReceipt> SetProofWindowRequestAndWaitForReceiptAsync(SetProofWindowFunction setProofWindowFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProofWindowFunction, cancellationToken);
        }

        public virtual Task<string> SetProofWindowRequestAsync(ulong chainId, BigInteger newWindow)
        {
            var setProofWindowFunction = new SetProofWindowFunction();
                setProofWindowFunction.ChainId = chainId;
                setProofWindowFunction.NewWindow = newWindow;
            
             return ContractHandler.SendRequestAsync(setProofWindowFunction);
        }

        public virtual Task<TransactionReceipt> SetProofWindowRequestAndWaitForReceiptAsync(ulong chainId, BigInteger newWindow, CancellationTokenSource cancellationToken = null)
        {
            var setProofWindowFunction = new SetProofWindowFunction();
                setProofWindowFunction.ChainId = chainId;
                setProofWindowFunction.NewWindow = newWindow;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProofWindowFunction, cancellationToken);
        }

        public virtual Task<string> SubmitBlockProofRequestAsync(SubmitBlockProofFunction submitBlockProofFunction)
        {
             return ContractHandler.SendRequestAsync(submitBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> SubmitBlockProofRequestAndWaitForReceiptAsync(SubmitBlockProofFunction submitBlockProofFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(submitBlockProofFunction, cancellationToken);
        }

        public virtual Task<string> SubmitBlockProofRequestAsync(ulong chainId, ulong blockNumber, ulong anchorEndBlock, byte[] blockHash, byte[] preStateRoot, byte[] postStateRoot, List<byte[]> merkleProof, byte[] zkProof, byte proofSystem)
        {
            var submitBlockProofFunction = new SubmitBlockProofFunction();
                submitBlockProofFunction.ChainId = chainId;
                submitBlockProofFunction.BlockNumber = blockNumber;
                submitBlockProofFunction.AnchorEndBlock = anchorEndBlock;
                submitBlockProofFunction.BlockHash = blockHash;
                submitBlockProofFunction.PreStateRoot = preStateRoot;
                submitBlockProofFunction.PostStateRoot = postStateRoot;
                submitBlockProofFunction.MerkleProof = merkleProof;
                submitBlockProofFunction.ZkProof = zkProof;
                submitBlockProofFunction.ProofSystem = proofSystem;
            
             return ContractHandler.SendRequestAsync(submitBlockProofFunction);
        }

        public virtual Task<TransactionReceipt> SubmitBlockProofRequestAndWaitForReceiptAsync(ulong chainId, ulong blockNumber, ulong anchorEndBlock, byte[] blockHash, byte[] preStateRoot, byte[] postStateRoot, List<byte[]> merkleProof, byte[] zkProof, byte proofSystem, CancellationTokenSource cancellationToken = null)
        {
            var submitBlockProofFunction = new SubmitBlockProofFunction();
                submitBlockProofFunction.ChainId = chainId;
                submitBlockProofFunction.BlockNumber = blockNumber;
                submitBlockProofFunction.AnchorEndBlock = anchorEndBlock;
                submitBlockProofFunction.BlockHash = blockHash;
                submitBlockProofFunction.PreStateRoot = preStateRoot;
                submitBlockProofFunction.PostStateRoot = postStateRoot;
                submitBlockProofFunction.MerkleProof = merkleProof;
                submitBlockProofFunction.ZkProof = zkProof;
                submitBlockProofFunction.ProofSystem = proofSystem;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(submitBlockProofFunction, cancellationToken);
        }

        public virtual Task<string> UnpauseRequestAsync(UnpauseFunction unpauseFunction)
        {
             return ContractHandler.SendRequestAsync(unpauseFunction);
        }

        public virtual Task<string> UnpauseRequestAsync()
        {
             return ContractHandler.SendRequestAsync<UnpauseFunction>();
        }

        public virtual Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(UnpauseFunction unpauseFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unpauseFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<UnpauseFunction>(null, cancellationToken);
        }

        public virtual Task<string> WithdrawBondRequestAsync(WithdrawBondFunction withdrawBondFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawBondFunction);
        }

        public virtual Task<string> WithdrawBondRequestAsync()
        {
             return ContractHandler.SendRequestAsync<WithdrawBondFunction>();
        }

        public virtual Task<TransactionReceipt> WithdrawBondRequestAndWaitForReceiptAsync(WithdrawBondFunction withdrawBondFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawBondFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> WithdrawBondRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<WithdrawBondFunction>(null, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MaxProofBondFunction),
                typeof(MaxProofWindowFunction),
                typeof(MinProofWindowFunction),
                typeof(AnchorFunction),
                typeof(BlockProofsFunction),
                typeof(ClaimProofTimeoutFunction),
                typeof(FulfillBlockProofFunction),
                typeof(GetBlockProofFunction),
                typeof(IsBlockProvenFunction),
                typeof(OwnerFunction),
                typeof(PauseFunction),
                typeof(PausedFunction),
                typeof(PendingWithdrawalsFunction),
                typeof(ProofBondFunction),
                typeof(ProofRequestsFunction),
                typeof(ProofWindowFunction),
                typeof(RequestBlockProofFunction),
                typeof(SetProofBondFunction),
                typeof(SetProofWindowFunction),
                typeof(SubmitBlockProofFunction),
                typeof(UnpauseFunction),
                typeof(WithdrawBondFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(BlockProofSubmittedEventDTO),
                typeof(BondCreditedEventDTO),
                typeof(BondWithdrawnEventDTO),
                typeof(PausedEventDTO),
                typeof(ProofBondChangedEventDTO),
                typeof(ProofRequestExpiredEventDTO),
                typeof(ProofRequestFulfilledEventDTO),
                typeof(ProofRequestedEventDTO),
                typeof(ProofWindowChangedEventDTO),
                typeof(UnpausedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(EnforcedPauseError),
                typeof(ExpectedPauseError),
                typeof(ReentrancyGuardReentrantCallError)
            };
        }
    }
}
