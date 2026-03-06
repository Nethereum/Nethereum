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
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition;

namespace Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy
{
    public partial class AppChainPolicyService: AppChainPolicyServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, AppChainPolicyDeployment appChainPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainPolicyDeployment>().SendRequestAndWaitForReceiptAsync(appChainPolicyDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, AppChainPolicyDeployment appChainPolicyDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainPolicyDeployment>().SendRequestAsync(appChainPolicyDeployment);
        }

        public static async Task<AppChainPolicyService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, AppChainPolicyDeployment appChainPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, appChainPolicyDeployment, cancellationTokenSource);
            return new AppChainPolicyService(web3, receipt.ContractAddress);
        }

        public AppChainPolicyService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class AppChainPolicyServiceBase: ContractWeb3ServiceBase
    {

        public AppChainPolicyServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> AdminsRootQueryAsync(AdminsRootFunction adminsRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AdminsRootFunction, byte[]>(adminsRootFunction, blockParameter);
        }

        
        public virtual Task<byte[]> AdminsRootQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AdminsRootFunction, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> AppChainIdQueryAsync(AppChainIdFunction appChainIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AppChainIdFunction, BigInteger>(appChainIdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> AppChainIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AppChainIdFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> BanRequestAsync(BanFunction banFunction)
        {
             return ContractHandler.SendRequestAsync(banFunction);
        }

        public virtual Task<TransactionReceipt> BanRequestAndWaitForReceiptAsync(BanFunction banFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(banFunction, cancellationToken);
        }

        public virtual Task<string> BanRequestAsync(string toBan, byte[] newBlacklistRoot, List<byte[]> proofCallerIsAdmin)
        {
            var banFunction = new BanFunction();
                banFunction.ToBan = toBan;
                banFunction.NewBlacklistRoot = newBlacklistRoot;
                banFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAsync(banFunction);
        }

        public virtual Task<TransactionReceipt> BanRequestAndWaitForReceiptAsync(string toBan, byte[] newBlacklistRoot, List<byte[]> proofCallerIsAdmin, CancellationTokenSource cancellationToken = null)
        {
            var banFunction = new BanFunction();
                banFunction.ToBan = toBan;
                banFunction.NewBlacklistRoot = newBlacklistRoot;
                banFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(banFunction, cancellationToken);
        }

        public Task<byte[]> BlacklistRootQueryAsync(BlacklistRootFunction blacklistRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BlacklistRootFunction, byte[]>(blacklistRootFunction, blockParameter);
        }

        
        public virtual Task<byte[]> BlacklistRootQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BlacklistRootFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<CurrentPolicyOutputDTO> CurrentPolicyQueryAsync(CurrentPolicyFunction currentPolicyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<CurrentPolicyFunction, CurrentPolicyOutputDTO>(currentPolicyFunction, blockParameter);
        }

        public virtual Task<CurrentPolicyOutputDTO> CurrentPolicyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<CurrentPolicyFunction, CurrentPolicyOutputDTO>(null, blockParameter);
        }

        public Task<BigInteger> EpochQueryAsync(EpochFunction epochFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EpochFunction, BigInteger>(epochFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> EpochQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EpochFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> InviteRequestAsync(InviteFunction inviteFunction)
        {
             return ContractHandler.SendRequestAsync(inviteFunction);
        }

        public virtual Task<TransactionReceipt> InviteRequestAndWaitForReceiptAsync(InviteFunction inviteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteFunction, cancellationToken);
        }

        public virtual Task<string> InviteRequestAsync(string invitee, byte[] newWritersRoot, List<byte[]> proofCallerIsWriter)
        {
            var inviteFunction = new InviteFunction();
                inviteFunction.Invitee = invitee;
                inviteFunction.NewWritersRoot = newWritersRoot;
                inviteFunction.ProofCallerIsWriter = proofCallerIsWriter;
            
             return ContractHandler.SendRequestAsync(inviteFunction);
        }

        public virtual Task<TransactionReceipt> InviteRequestAndWaitForReceiptAsync(string invitee, byte[] newWritersRoot, List<byte[]> proofCallerIsWriter, CancellationTokenSource cancellationToken = null)
        {
            var inviteFunction = new InviteFunction();
                inviteFunction.Invitee = invitee;
                inviteFunction.NewWritersRoot = newWritersRoot;
                inviteFunction.ProofCallerIsWriter = proofCallerIsWriter;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteFunction, cancellationToken);
        }

        public Task<bool> IsValidWriterQueryAsync(IsValidWriterFunction isValidWriterFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidWriterFunction, bool>(isValidWriterFunction, blockParameter);
        }

        
        public virtual Task<bool> IsValidWriterQueryAsync(string addr, List<byte[]> writerProof, List<byte[]> blacklistProof, BlockParameter blockParameter = null)
        {
            var isValidWriterFunction = new IsValidWriterFunction();
                isValidWriterFunction.Addr = addr;
                isValidWriterFunction.WriterProof = writerProof;
                isValidWriterFunction.BlacklistProof = blacklistProof;
            
            return ContractHandler.QueryAsync<IsValidWriterFunction, bool>(isValidWriterFunction, blockParameter);
        }

        public virtual Task<string> RebuildTreesRequestAsync(RebuildTreesFunction rebuildTreesFunction)
        {
             return ContractHandler.SendRequestAsync(rebuildTreesFunction);
        }

        public virtual Task<TransactionReceipt> RebuildTreesRequestAndWaitForReceiptAsync(RebuildTreesFunction rebuildTreesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(rebuildTreesFunction, cancellationToken);
        }

        public virtual Task<string> RebuildTreesRequestAsync(byte[] newWritersRoot, byte[] newAdminsRoot, List<byte[]> proofCallerIsAdmin)
        {
            var rebuildTreesFunction = new RebuildTreesFunction();
                rebuildTreesFunction.NewWritersRoot = newWritersRoot;
                rebuildTreesFunction.NewAdminsRoot = newAdminsRoot;
                rebuildTreesFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAsync(rebuildTreesFunction);
        }

        public virtual Task<TransactionReceipt> RebuildTreesRequestAndWaitForReceiptAsync(byte[] newWritersRoot, byte[] newAdminsRoot, List<byte[]> proofCallerIsAdmin, CancellationTokenSource cancellationToken = null)
        {
            var rebuildTreesFunction = new RebuildTreesFunction();
                rebuildTreesFunction.NewWritersRoot = newWritersRoot;
                rebuildTreesFunction.NewAdminsRoot = newAdminsRoot;
                rebuildTreesFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(rebuildTreesFunction, cancellationToken);
        }

        public virtual Task<string> UpdatePolicyRequestAsync(UpdatePolicyFunction updatePolicyFunction)
        {
             return ContractHandler.SendRequestAsync(updatePolicyFunction);
        }

        public virtual Task<TransactionReceipt> UpdatePolicyRequestAndWaitForReceiptAsync(UpdatePolicyFunction updatePolicyFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updatePolicyFunction, cancellationToken);
        }

        public virtual Task<string> UpdatePolicyRequestAsync(BigInteger maxCalldataBytes, BigInteger maxLogBytes, BigInteger blockGasLimit, string sequencer, List<byte[]> proofCallerIsAdmin)
        {
            var updatePolicyFunction = new UpdatePolicyFunction();
                updatePolicyFunction.MaxCalldataBytes = maxCalldataBytes;
                updatePolicyFunction.MaxLogBytes = maxLogBytes;
                updatePolicyFunction.BlockGasLimit = blockGasLimit;
                updatePolicyFunction.Sequencer = sequencer;
                updatePolicyFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAsync(updatePolicyFunction);
        }

        public virtual Task<TransactionReceipt> UpdatePolicyRequestAndWaitForReceiptAsync(BigInteger maxCalldataBytes, BigInteger maxLogBytes, BigInteger blockGasLimit, string sequencer, List<byte[]> proofCallerIsAdmin, CancellationTokenSource cancellationToken = null)
        {
            var updatePolicyFunction = new UpdatePolicyFunction();
                updatePolicyFunction.MaxCalldataBytes = maxCalldataBytes;
                updatePolicyFunction.MaxLogBytes = maxLogBytes;
                updatePolicyFunction.BlockGasLimit = blockGasLimit;
                updatePolicyFunction.Sequencer = sequencer;
                updatePolicyFunction.ProofCallerIsAdmin = proofCallerIsAdmin;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updatePolicyFunction, cancellationToken);
        }

        public Task<byte[]> WritersRootQueryAsync(WritersRootFunction writersRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WritersRootFunction, byte[]>(writersRootFunction, blockParameter);
        }

        
        public virtual Task<byte[]> WritersRootQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WritersRootFunction, byte[]>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AdminsRootFunction),
                typeof(AppChainIdFunction),
                typeof(BanFunction),
                typeof(BlacklistRootFunction),
                typeof(CurrentPolicyFunction),
                typeof(EpochFunction),
                typeof(InviteFunction),
                typeof(IsValidWriterFunction),
                typeof(RebuildTreesFunction),
                typeof(UpdatePolicyFunction),
                typeof(WritersRootFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AdminAddedEventDTO),
                typeof(MemberBannedEventDTO),
                typeof(MemberInvitedEventDTO),
                typeof(PolicyChangedEventDTO),
                typeof(TreeRebuiltEventDTO)
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
