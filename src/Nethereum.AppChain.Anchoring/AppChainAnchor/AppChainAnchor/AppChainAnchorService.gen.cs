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
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor
{
    public partial class AppChainAnchorService: AppChainAnchorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainAnchorDeployment>().SendRequestAndWaitForReceiptAsync(appChainAnchorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainAnchorDeployment>().SendRequestAsync(appChainAnchorDeployment);
        }

        public static async Task<AppChainAnchorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, appChainAnchorDeployment, cancellationTokenSource);
            return new AppChainAnchorService(web3, receipt.ContractAddress);
        }

        public AppChainAnchorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class AppChainAnchorServiceBase: ContractWeb3ServiceBase
    {

        public AppChainAnchorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> MaxProofSizeQueryAsync(MaxProofSizeFunction maxProofSizeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofSizeFunction, BigInteger>(maxProofSizeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxProofSizeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxProofSizeFunction, BigInteger>(null, blockParameter);
        }

        public Task<byte[]> AnchorCommitmentsQueryAsync(AnchorCommitmentsFunction anchorCommitmentsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AnchorCommitmentsFunction, byte[]>(anchorCommitmentsFunction, blockParameter);
        }

        
        public virtual Task<byte[]> AnchorCommitmentsQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var anchorCommitmentsFunction = new AnchorCommitmentsFunction();
                anchorCommitmentsFunction.ReturnValue1 = returnValue1;
                anchorCommitmentsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<AnchorCommitmentsFunction, byte[]>(anchorCommitmentsFunction, blockParameter);
        }

        public virtual Task<AppChainsOutputDTO> AppChainsQueryAsync(AppChainsFunction appChainsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AppChainsFunction, AppChainsOutputDTO>(appChainsFunction, blockParameter);
        }

        public virtual Task<AppChainsOutputDTO> AppChainsQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var appChainsFunction = new AppChainsFunction();
                appChainsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AppChainsFunction, AppChainsOutputDTO>(appChainsFunction, blockParameter);
        }

        public Task<byte[]> BlockHashesRootsQueryAsync(BlockHashesRootsFunction blockHashesRootsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BlockHashesRootsFunction, byte[]>(blockHashesRootsFunction, blockParameter);
        }

        
        public virtual Task<byte[]> BlockHashesRootsQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var blockHashesRootsFunction = new BlockHashesRootsFunction();
                blockHashesRootsFunction.ReturnValue1 = returnValue1;
                blockHashesRootsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<BlockHashesRootsFunction, byte[]>(blockHashesRootsFunction, blockParameter);
        }

        public Task<ulong> ChainIdByGenesisQueryAsync(ChainIdByGenesisFunction chainIdByGenesisFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ChainIdByGenesisFunction, ulong>(chainIdByGenesisFunction, blockParameter);
        }

        
        public virtual Task<ulong> ChainIdByGenesisQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var chainIdByGenesisFunction = new ChainIdByGenesisFunction();
                chainIdByGenesisFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ChainIdByGenesisFunction, ulong>(chainIdByGenesisFunction, blockParameter);
        }

        public virtual Task<GetAppChainConfigOutputDTO> GetAppChainConfigQueryAsync(GetAppChainConfigFunction getAppChainConfigFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetAppChainConfigFunction, GetAppChainConfigOutputDTO>(getAppChainConfigFunction, blockParameter);
        }

        public virtual Task<GetAppChainConfigOutputDTO> GetAppChainConfigQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var getAppChainConfigFunction = new GetAppChainConfigFunction();
                getAppChainConfigFunction.ChainId = chainId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetAppChainConfigFunction, GetAppChainConfigOutputDTO>(getAppChainConfigFunction, blockParameter);
        }

        public Task<string> GetChainAuthorityQueryAsync(GetChainAuthorityFunction getChainAuthorityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetChainAuthorityFunction, string>(getChainAuthorityFunction, blockParameter);
        }

        
        public virtual Task<string> GetChainAuthorityQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var getChainAuthorityFunction = new GetChainAuthorityFunction();
                getChainAuthorityFunction.ChainId = chainId;
            
            return ContractHandler.QueryAsync<GetChainAuthorityFunction, string>(getChainAuthorityFunction, blockParameter);
        }

        public virtual Task<GetLatestAnchorOutputDTO> GetLatestAnchorQueryAsync(GetLatestAnchorFunction getLatestAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetLatestAnchorFunction, GetLatestAnchorOutputDTO>(getLatestAnchorFunction, blockParameter);
        }

        public virtual Task<GetLatestAnchorOutputDTO> GetLatestAnchorQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var getLatestAnchorFunction = new GetLatestAnchorFunction();
                getLatestAnchorFunction.ChainId = chainId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetLatestAnchorFunction, GetLatestAnchorOutputDTO>(getLatestAnchorFunction, blockParameter);
        }

        public virtual Task<LatestAnchorOutputDTO> LatestAnchorQueryAsync(LatestAnchorFunction latestAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<LatestAnchorFunction, LatestAnchorOutputDTO>(latestAnchorFunction, blockParameter);
        }

        public virtual Task<LatestAnchorOutputDTO> LatestAnchorQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var latestAnchorFunction = new LatestAnchorFunction();
                latestAnchorFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<LatestAnchorFunction, LatestAnchorOutputDTO>(latestAnchorFunction, blockParameter);
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

        public virtual Task<ProofSystemsOutputDTO> ProofSystemsQueryAsync(ProofSystemsFunction proofSystemsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ProofSystemsFunction, ProofSystemsOutputDTO>(proofSystemsFunction, blockParameter);
        }

        public virtual Task<ProofSystemsOutputDTO> ProofSystemsQueryAsync(byte returnValue1, BlockParameter blockParameter = null)
        {
            var proofSystemsFunction = new ProofSystemsFunction();
                proofSystemsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ProofSystemsFunction, ProofSystemsOutputDTO>(proofSystemsFunction, blockParameter);
        }

        public virtual Task<string> RaiseMinimumAnchorVersionRequestAsync(RaiseMinimumAnchorVersionFunction raiseMinimumAnchorVersionFunction)
        {
             return ContractHandler.SendRequestAsync(raiseMinimumAnchorVersionFunction);
        }

        public virtual Task<TransactionReceipt> RaiseMinimumAnchorVersionRequestAndWaitForReceiptAsync(RaiseMinimumAnchorVersionFunction raiseMinimumAnchorVersionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(raiseMinimumAnchorVersionFunction, cancellationToken);
        }

        public virtual Task<string> RaiseMinimumAnchorVersionRequestAsync(ulong chainId, byte newFloor)
        {
            var raiseMinimumAnchorVersionFunction = new RaiseMinimumAnchorVersionFunction();
                raiseMinimumAnchorVersionFunction.ChainId = chainId;
                raiseMinimumAnchorVersionFunction.NewFloor = newFloor;
            
             return ContractHandler.SendRequestAsync(raiseMinimumAnchorVersionFunction);
        }

        public virtual Task<TransactionReceipt> RaiseMinimumAnchorVersionRequestAndWaitForReceiptAsync(ulong chainId, byte newFloor, CancellationTokenSource cancellationToken = null)
        {
            var raiseMinimumAnchorVersionFunction = new RaiseMinimumAnchorVersionFunction();
                raiseMinimumAnchorVersionFunction.ChainId = chainId;
                raiseMinimumAnchorVersionFunction.NewFloor = newFloor;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(raiseMinimumAnchorVersionFunction, cancellationToken);
        }

        public virtual Task<string> RaiseMinimumProofSystemRequestAsync(RaiseMinimumProofSystemFunction raiseMinimumProofSystemFunction)
        {
             return ContractHandler.SendRequestAsync(raiseMinimumProofSystemFunction);
        }

        public virtual Task<TransactionReceipt> RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(RaiseMinimumProofSystemFunction raiseMinimumProofSystemFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(raiseMinimumProofSystemFunction, cancellationToken);
        }

        public virtual Task<string> RaiseMinimumProofSystemRequestAsync(ulong chainId, byte newFloor)
        {
            var raiseMinimumProofSystemFunction = new RaiseMinimumProofSystemFunction();
                raiseMinimumProofSystemFunction.ChainId = chainId;
                raiseMinimumProofSystemFunction.NewFloor = newFloor;
            
             return ContractHandler.SendRequestAsync(raiseMinimumProofSystemFunction);
        }

        public virtual Task<TransactionReceipt> RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(ulong chainId, byte newFloor, CancellationTokenSource cancellationToken = null)
        {
            var raiseMinimumProofSystemFunction = new RaiseMinimumProofSystemFunction();
                raiseMinimumProofSystemFunction.ChainId = chainId;
                raiseMinimumProofSystemFunction.NewFloor = newFloor;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(raiseMinimumProofSystemFunction, cancellationToken);
        }

        public virtual Task<string> RegisterAppChainRequestAsync(RegisterAppChainFunction registerAppChainFunction)
        {
             return ContractHandler.SendRequestAsync(registerAppChainFunction);
        }

        public virtual Task<TransactionReceipt> RegisterAppChainRequestAndWaitForReceiptAsync(RegisterAppChainFunction registerAppChainFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerAppChainFunction, cancellationToken);
        }

        public virtual Task<string> RegisterAppChainRequestAsync(ulong chainId, byte[] genesisHash, ulong genesisBlock, byte[] genesisStateRoot, byte minimumProofSystem, byte minimumAnchorVersion, string authority)
        {
            var registerAppChainFunction = new RegisterAppChainFunction();
                registerAppChainFunction.ChainId = chainId;
                registerAppChainFunction.GenesisHash = genesisHash;
                registerAppChainFunction.GenesisBlock = genesisBlock;
                registerAppChainFunction.GenesisStateRoot = genesisStateRoot;
                registerAppChainFunction.MinimumProofSystem = minimumProofSystem;
                registerAppChainFunction.MinimumAnchorVersion = minimumAnchorVersion;
                registerAppChainFunction.Authority = authority;
            
             return ContractHandler.SendRequestAsync(registerAppChainFunction);
        }

        public virtual Task<TransactionReceipt> RegisterAppChainRequestAndWaitForReceiptAsync(ulong chainId, byte[] genesisHash, ulong genesisBlock, byte[] genesisStateRoot, byte minimumProofSystem, byte minimumAnchorVersion, string authority, CancellationTokenSource cancellationToken = null)
        {
            var registerAppChainFunction = new RegisterAppChainFunction();
                registerAppChainFunction.ChainId = chainId;
                registerAppChainFunction.GenesisHash = genesisHash;
                registerAppChainFunction.GenesisBlock = genesisBlock;
                registerAppChainFunction.GenesisStateRoot = genesisStateRoot;
                registerAppChainFunction.MinimumProofSystem = minimumProofSystem;
                registerAppChainFunction.MinimumAnchorVersion = minimumAnchorVersion;
                registerAppChainFunction.Authority = authority;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerAppChainFunction, cancellationToken);
        }

        public virtual Task<string> RegisterProofSystemRequestAsync(RegisterProofSystemFunction registerProofSystemFunction)
        {
             return ContractHandler.SendRequestAsync(registerProofSystemFunction);
        }

        public virtual Task<TransactionReceipt> RegisterProofSystemRequestAndWaitForReceiptAsync(RegisterProofSystemFunction registerProofSystemFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerProofSystemFunction, cancellationToken);
        }

        public virtual Task<string> RegisterProofSystemRequestAsync(byte proofSystem, string verifier, bool requiresProof)
        {
            var registerProofSystemFunction = new RegisterProofSystemFunction();
                registerProofSystemFunction.ProofSystem = proofSystem;
                registerProofSystemFunction.Verifier = verifier;
                registerProofSystemFunction.RequiresProof = requiresProof;
            
             return ContractHandler.SendRequestAsync(registerProofSystemFunction);
        }

        public virtual Task<TransactionReceipt> RegisterProofSystemRequestAndWaitForReceiptAsync(byte proofSystem, string verifier, bool requiresProof, CancellationTokenSource cancellationToken = null)
        {
            var registerProofSystemFunction = new RegisterProofSystemFunction();
                registerProofSystemFunction.ProofSystem = proofSystem;
                registerProofSystemFunction.Verifier = verifier;
                registerProofSystemFunction.RequiresProof = requiresProof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerProofSystemFunction, cancellationToken);
        }

        public virtual Task<string> RegisterSchemaRequestAsync(RegisterSchemaFunction registerSchemaFunction)
        {
             return ContractHandler.SendRequestAsync(registerSchemaFunction);
        }

        public virtual Task<TransactionReceipt> RegisterSchemaRequestAndWaitForReceiptAsync(RegisterSchemaFunction registerSchemaFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSchemaFunction, cancellationToken);
        }

        public virtual Task<string> RegisterSchemaRequestAsync(byte version, byte[] hashFunction, byte trieType, byte stateModel)
        {
            var registerSchemaFunction = new RegisterSchemaFunction();
                registerSchemaFunction.Version = version;
                registerSchemaFunction.HashFunction = hashFunction;
                registerSchemaFunction.TrieType = trieType;
                registerSchemaFunction.StateModel = stateModel;
            
             return ContractHandler.SendRequestAsync(registerSchemaFunction);
        }

        public virtual Task<TransactionReceipt> RegisterSchemaRequestAndWaitForReceiptAsync(byte version, byte[] hashFunction, byte trieType, byte stateModel, CancellationTokenSource cancellationToken = null)
        {
            var registerSchemaFunction = new RegisterSchemaFunction();
                registerSchemaFunction.Version = version;
                registerSchemaFunction.HashFunction = hashFunction;
                registerSchemaFunction.TrieType = trieType;
                registerSchemaFunction.StateModel = stateModel;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSchemaFunction, cancellationToken);
        }

        public Task<bool> SchemaExistsQueryAsync(SchemaExistsFunction schemaExistsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SchemaExistsFunction, bool>(schemaExistsFunction, blockParameter);
        }

        
        public virtual Task<bool> SchemaExistsQueryAsync(byte returnValue1, BlockParameter blockParameter = null)
        {
            var schemaExistsFunction = new SchemaExistsFunction();
                schemaExistsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<SchemaExistsFunction, bool>(schemaExistsFunction, blockParameter);
        }

        public virtual Task<string> SetChainAuthorityRequestAsync(SetChainAuthorityFunction setChainAuthorityFunction)
        {
             return ContractHandler.SendRequestAsync(setChainAuthorityFunction);
        }

        public virtual Task<TransactionReceipt> SetChainAuthorityRequestAndWaitForReceiptAsync(SetChainAuthorityFunction setChainAuthorityFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setChainAuthorityFunction, cancellationToken);
        }

        public virtual Task<string> SetChainAuthorityRequestAsync(ulong chainId, string newAuthority)
        {
            var setChainAuthorityFunction = new SetChainAuthorityFunction();
                setChainAuthorityFunction.ChainId = chainId;
                setChainAuthorityFunction.NewAuthority = newAuthority;
            
             return ContractHandler.SendRequestAsync(setChainAuthorityFunction);
        }

        public virtual Task<TransactionReceipt> SetChainAuthorityRequestAndWaitForReceiptAsync(ulong chainId, string newAuthority, CancellationTokenSource cancellationToken = null)
        {
            var setChainAuthorityFunction = new SetChainAuthorityFunction();
                setChainAuthorityFunction.ChainId = chainId;
                setChainAuthorityFunction.NewAuthority = newAuthority;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setChainAuthorityFunction, cancellationToken);
        }

        public virtual Task<string> SubmitAnchorRequestAsync(SubmitAnchorFunction submitAnchorFunction)
        {
             return ContractHandler.SendRequestAsync(submitAnchorFunction);
        }

        public virtual Task<TransactionReceipt> SubmitAnchorRequestAndWaitForReceiptAsync(SubmitAnchorFunction submitAnchorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(submitAnchorFunction, cancellationToken);
        }

        public virtual Task<string> SubmitAnchorRequestAsync(AggregatedAnchor a, byte[] proof)
        {
            var submitAnchorFunction = new SubmitAnchorFunction();
                submitAnchorFunction.A = a;
                submitAnchorFunction.Proof = proof;
            
             return ContractHandler.SendRequestAsync(submitAnchorFunction);
        }

        public virtual Task<TransactionReceipt> SubmitAnchorRequestAndWaitForReceiptAsync(AggregatedAnchor a, byte[] proof, CancellationTokenSource cancellationToken = null)
        {
            var submitAnchorFunction = new SubmitAnchorFunction();
                submitAnchorFunction.A = a;
                submitAnchorFunction.Proof = proof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(submitAnchorFunction, cancellationToken);
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

        public Task<bool> VerifyBlockInclusionQueryAsync(VerifyBlockInclusionFunction verifyBlockInclusionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyBlockInclusionFunction, bool>(verifyBlockInclusionFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyBlockInclusionQueryAsync(ulong chainId, ulong anchorEndBlock, ulong blockNumber, byte[] blockHash, byte[] preStateRoot, byte[] postStateRoot, List<byte[]> merkleProof, BlockParameter blockParameter = null)
        {
            var verifyBlockInclusionFunction = new VerifyBlockInclusionFunction();
                verifyBlockInclusionFunction.ChainId = chainId;
                verifyBlockInclusionFunction.AnchorEndBlock = anchorEndBlock;
                verifyBlockInclusionFunction.BlockNumber = blockNumber;
                verifyBlockInclusionFunction.BlockHash = blockHash;
                verifyBlockInclusionFunction.PreStateRoot = preStateRoot;
                verifyBlockInclusionFunction.PostStateRoot = postStateRoot;
                verifyBlockInclusionFunction.MerkleProof = merkleProof;
            
            return ContractHandler.QueryAsync<VerifyBlockInclusionFunction, bool>(verifyBlockInclusionFunction, blockParameter);
        }

        public Task<bool> VerifyStateRootQueryAsync(VerifyStateRootFunction verifyStateRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyStateRootFunction, bool>(verifyStateRootFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyStateRootQueryAsync(ulong chainId, byte[] stateRoot, BlockParameter blockParameter = null)
        {
            var verifyStateRootFunction = new VerifyStateRootFunction();
                verifyStateRootFunction.ChainId = chainId;
                verifyStateRootFunction.StateRoot = stateRoot;
            
            return ContractHandler.QueryAsync<VerifyStateRootFunction, bool>(verifyStateRootFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MaxProofSizeFunction),
                typeof(AnchorCommitmentsFunction),
                typeof(AppChainsFunction),
                typeof(BlockHashesRootsFunction),
                typeof(ChainIdByGenesisFunction),
                typeof(GetAppChainConfigFunction),
                typeof(GetChainAuthorityFunction),
                typeof(GetLatestAnchorFunction),
                typeof(LatestAnchorFunction),
                typeof(OwnerFunction),
                typeof(PauseFunction),
                typeof(PausedFunction),
                typeof(ProofSystemsFunction),
                typeof(RaiseMinimumAnchorVersionFunction),
                typeof(RaiseMinimumProofSystemFunction),
                typeof(RegisterAppChainFunction),
                typeof(RegisterProofSystemFunction),
                typeof(RegisterSchemaFunction),
                typeof(SchemaExistsFunction),
                typeof(SetChainAuthorityFunction),
                typeof(SubmitAnchorFunction),
                typeof(TransferOwnershipFunction),
                typeof(UnpauseFunction),
                typeof(VerifyBlockInclusionFunction),
                typeof(VerifyStateRootFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AnchorSubmittedEventDTO),
                typeof(AppChainRegisteredEventDTO),
                typeof(ChainAuthorityChangedEventDTO),
                typeof(MinimumAnchorVersionRaisedEventDTO),
                typeof(MinimumProofSystemRaisedEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(PausedEventDTO),
                typeof(ProofSystemRegisteredEventDTO),
                typeof(SchemaRegisteredEventDTO),
                typeof(UnpausedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(EnforcedPauseError),
                typeof(ExpectedPauseError)
            };
        }
    }
}
