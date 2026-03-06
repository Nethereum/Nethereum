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
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub
{
    public partial class AppChainHubService: AppChainHubServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, AppChainHubDeployment appChainHubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainHubDeployment>().SendRequestAndWaitForReceiptAsync(appChainHubDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, AppChainHubDeployment appChainHubDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainHubDeployment>().SendRequestAsync(appChainHubDeployment);
        }

        public static async Task<AppChainHubService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, AppChainHubDeployment appChainHubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, appChainHubDeployment, cancellationTokenSource);
            return new AppChainHubService(web3, receipt.ContractAddress);
        }

        public AppChainHubService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class AppChainHubServiceBase: ContractWeb3ServiceBase
    {

        public AppChainHubServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<ulong> MaxChainIdQueryAsync(MaxChainIdFunction maxChainIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxChainIdFunction, ulong>(maxChainIdFunction, blockParameter);
        }

        
        public virtual Task<ulong> MaxChainIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxChainIdFunction, ulong>(null, blockParameter);
        }

        public Task<BigInteger> MaxMessageSizeQueryAsync(MaxMessageSizeFunction maxMessageSizeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxMessageSizeFunction, BigInteger>(maxMessageSizeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxMessageSizeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxMessageSizeFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> AcknowledgeMessagesRequestAsync(AcknowledgeMessagesFunction acknowledgeMessagesFunction)
        {
             return ContractHandler.SendRequestAsync(acknowledgeMessagesFunction);
        }

        public virtual Task<TransactionReceipt> AcknowledgeMessagesRequestAndWaitForReceiptAsync(AcknowledgeMessagesFunction acknowledgeMessagesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acknowledgeMessagesFunction, cancellationToken);
        }

        public virtual Task<string> AcknowledgeMessagesRequestAsync(ulong chainId, ulong processedUpToMessageId, byte[] messagesRoot)
        {
            var acknowledgeMessagesFunction = new AcknowledgeMessagesFunction();
                acknowledgeMessagesFunction.ChainId = chainId;
                acknowledgeMessagesFunction.ProcessedUpToMessageId = processedUpToMessageId;
                acknowledgeMessagesFunction.MessagesRoot = messagesRoot;
            
             return ContractHandler.SendRequestAsync(acknowledgeMessagesFunction);
        }

        public virtual Task<TransactionReceipt> AcknowledgeMessagesRequestAndWaitForReceiptAsync(ulong chainId, ulong processedUpToMessageId, byte[] messagesRoot, CancellationTokenSource cancellationToken = null)
        {
            var acknowledgeMessagesFunction = new AcknowledgeMessagesFunction();
                acknowledgeMessagesFunction.ChainId = chainId;
                acknowledgeMessagesFunction.ProcessedUpToMessageId = processedUpToMessageId;
                acknowledgeMessagesFunction.MessagesRoot = messagesRoot;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acknowledgeMessagesFunction, cancellationToken);
        }

        public virtual Task<string> AnchorRequestAsync(AnchorFunction anchorFunction)
        {
             return ContractHandler.SendRequestAsync(anchorFunction);
        }

        public virtual Task<TransactionReceipt> AnchorRequestAndWaitForReceiptAsync(AnchorFunction anchorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(anchorFunction, cancellationToken);
        }

        public virtual Task<string> AnchorRequestAsync(ulong chainId, ulong blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot, ulong processedUpToMessageId, byte[] extraData)
        {
            var anchorFunction = new AnchorFunction();
                anchorFunction.ChainId = chainId;
                anchorFunction.BlockNumber = blockNumber;
                anchorFunction.StateRoot = stateRoot;
                anchorFunction.TxRoot = txRoot;
                anchorFunction.ReceiptRoot = receiptRoot;
                anchorFunction.ProcessedUpToMessageId = processedUpToMessageId;
                anchorFunction.ExtraData = extraData;
            
             return ContractHandler.SendRequestAsync(anchorFunction);
        }

        public virtual Task<TransactionReceipt> AnchorRequestAndWaitForReceiptAsync(ulong chainId, ulong blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot, ulong processedUpToMessageId, byte[] extraData, CancellationTokenSource cancellationToken = null)
        {
            var anchorFunction = new AnchorFunction();
                anchorFunction.ChainId = chainId;
                anchorFunction.BlockNumber = blockNumber;
                anchorFunction.StateRoot = stateRoot;
                anchorFunction.TxRoot = txRoot;
                anchorFunction.ReceiptRoot = receiptRoot;
                anchorFunction.ProcessedUpToMessageId = processedUpToMessageId;
                anchorFunction.ExtraData = extraData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(anchorFunction, cancellationToken);
        }

        public virtual Task<AnchorsOutputDTO> AnchorsQueryAsync(AnchorsFunction anchorsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AnchorsFunction, AnchorsOutputDTO>(anchorsFunction, blockParameter);
        }

        public virtual Task<AnchorsOutputDTO> AnchorsQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var anchorsFunction = new AnchorsFunction();
                anchorsFunction.ReturnValue1 = returnValue1;
                anchorsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AnchorsFunction, AnchorsOutputDTO>(anchorsFunction, blockParameter);
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

        public Task<bool> AuthorizedSendersQueryAsync(AuthorizedSendersFunction authorizedSendersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AuthorizedSendersFunction, bool>(authorizedSendersFunction, blockParameter);
        }

        
        public virtual Task<bool> AuthorizedSendersQueryAsync(ulong returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var authorizedSendersFunction = new AuthorizedSendersFunction();
                authorizedSendersFunction.ReturnValue1 = returnValue1;
                authorizedSendersFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<AuthorizedSendersFunction, bool>(authorizedSendersFunction, blockParameter);
        }

        public virtual Task<GetAnchorOutputDTO> GetAnchorQueryAsync(GetAnchorFunction getAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetAnchorFunction, GetAnchorOutputDTO>(getAnchorFunction, blockParameter);
        }

        public virtual Task<GetAnchorOutputDTO> GetAnchorQueryAsync(ulong chainId, ulong blockNumber, BlockParameter blockParameter = null)
        {
            var getAnchorFunction = new GetAnchorFunction();
                getAnchorFunction.ChainId = chainId;
                getAnchorFunction.BlockNumber = blockNumber;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetAnchorFunction, GetAnchorOutputDTO>(getAnchorFunction, blockParameter);
        }

        public virtual Task<GetAppChainInfoOutputDTO> GetAppChainInfoQueryAsync(GetAppChainInfoFunction getAppChainInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetAppChainInfoFunction, GetAppChainInfoOutputDTO>(getAppChainInfoFunction, blockParameter);
        }

        public virtual Task<GetAppChainInfoOutputDTO> GetAppChainInfoQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var getAppChainInfoFunction = new GetAppChainInfoFunction();
                getAppChainInfoFunction.ChainId = chainId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetAppChainInfoFunction, GetAppChainInfoOutputDTO>(getAppChainInfoFunction, blockParameter);
        }

        public virtual Task<GetMessageOutputDTO> GetMessageQueryAsync(GetMessageFunction getMessageFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageFunction, GetMessageOutputDTO>(getMessageFunction, blockParameter);
        }

        public virtual Task<GetMessageOutputDTO> GetMessageQueryAsync(ulong chainId, ulong messageId, BlockParameter blockParameter = null)
        {
            var getMessageFunction = new GetMessageFunction();
                getMessageFunction.ChainId = chainId;
                getMessageFunction.MessageId = messageId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageFunction, GetMessageOutputDTO>(getMessageFunction, blockParameter);
        }

        public virtual Task<GetMessageRangeOutputDTO> GetMessageRangeQueryAsync(GetMessageRangeFunction getMessageRangeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageRangeFunction, GetMessageRangeOutputDTO>(getMessageRangeFunction, blockParameter);
        }

        public virtual Task<GetMessageRangeOutputDTO> GetMessageRangeQueryAsync(ulong chainId, ulong fromId, ulong toId, BlockParameter blockParameter = null)
        {
            var getMessageRangeFunction = new GetMessageRangeFunction();
                getMessageRangeFunction.ChainId = chainId;
                getMessageRangeFunction.FromId = fromId;
                getMessageRangeFunction.ToId = toId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageRangeFunction, GetMessageRangeOutputDTO>(getMessageRangeFunction, blockParameter);
        }

        public virtual Task<GetMessageRootCheckpointOutputDTO> GetMessageRootCheckpointQueryAsync(GetMessageRootCheckpointFunction getMessageRootCheckpointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageRootCheckpointFunction, GetMessageRootCheckpointOutputDTO>(getMessageRootCheckpointFunction, blockParameter);
        }

        public virtual Task<GetMessageRootCheckpointOutputDTO> GetMessageRootCheckpointQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var getMessageRootCheckpointFunction = new GetMessageRootCheckpointFunction();
                getMessageRootCheckpointFunction.ChainId = chainId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetMessageRootCheckpointFunction, GetMessageRootCheckpointOutputDTO>(getMessageRootCheckpointFunction, blockParameter);
        }

        public Task<BigInteger> HubBalanceQueryAsync(HubBalanceFunction hubBalanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubBalanceFunction, BigInteger>(hubBalanceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> HubBalanceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubBalanceFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> HubFeeBpsQueryAsync(HubFeeBpsFunction hubFeeBpsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFeeBpsFunction, BigInteger>(hubFeeBpsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> HubFeeBpsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFeeBpsFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> HubOwnerQueryAsync(HubOwnerFunction hubOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubOwnerFunction, string>(hubOwnerFunction, blockParameter);
        }

        
        public virtual Task<string> HubOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubOwnerFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MessageFeeQueryAsync(MessageFeeFunction messageFeeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessageFeeFunction, BigInteger>(messageFeeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MessageFeeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessageFeeFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<MessageRootCheckpointsOutputDTO> MessageRootCheckpointsQueryAsync(MessageRootCheckpointsFunction messageRootCheckpointsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<MessageRootCheckpointsFunction, MessageRootCheckpointsOutputDTO>(messageRootCheckpointsFunction, blockParameter);
        }

        public virtual Task<MessageRootCheckpointsOutputDTO> MessageRootCheckpointsQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var messageRootCheckpointsFunction = new MessageRootCheckpointsFunction();
                messageRootCheckpointsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<MessageRootCheckpointsFunction, MessageRootCheckpointsOutputDTO>(messageRootCheckpointsFunction, blockParameter);
        }

        public virtual Task<MessagesOutputDTO> MessagesQueryAsync(MessagesFunction messagesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<MessagesFunction, MessagesOutputDTO>(messagesFunction, blockParameter);
        }

        public virtual Task<MessagesOutputDTO> MessagesQueryAsync(ulong returnValue1, ulong returnValue2, BlockParameter blockParameter = null)
        {
            var messagesFunction = new MessagesFunction();
                messagesFunction.ReturnValue1 = returnValue1;
                messagesFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<MessagesFunction, MessagesOutputDTO>(messagesFunction, blockParameter);
        }

        public Task<BigInteger> OwnerBalancesQueryAsync(OwnerBalancesFunction ownerBalancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerBalancesFunction, BigInteger>(ownerBalancesFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> OwnerBalancesQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var ownerBalancesFunction = new OwnerBalancesFunction();
                ownerBalancesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<OwnerBalancesFunction, BigInteger>(ownerBalancesFunction, blockParameter);
        }

        public Task<ulong> PendingMessageCountQueryAsync(PendingMessageCountFunction pendingMessageCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PendingMessageCountFunction, ulong>(pendingMessageCountFunction, blockParameter);
        }

        
        public virtual Task<ulong> PendingMessageCountQueryAsync(ulong chainId, BlockParameter blockParameter = null)
        {
            var pendingMessageCountFunction = new PendingMessageCountFunction();
                pendingMessageCountFunction.ChainId = chainId;
            
            return ContractHandler.QueryAsync<PendingMessageCountFunction, ulong>(pendingMessageCountFunction, blockParameter);
        }

        public virtual Task<string> RegisterAppChainRequestAsync(RegisterAppChainFunction registerAppChainFunction)
        {
             return ContractHandler.SendRequestAsync(registerAppChainFunction);
        }

        public virtual Task<TransactionReceipt> RegisterAppChainRequestAndWaitForReceiptAsync(RegisterAppChainFunction registerAppChainFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerAppChainFunction, cancellationToken);
        }

        public virtual Task<string> RegisterAppChainRequestAsync(ulong chainId, string sequencer, byte[] sequencerSignature)
        {
            var registerAppChainFunction = new RegisterAppChainFunction();
                registerAppChainFunction.ChainId = chainId;
                registerAppChainFunction.Sequencer = sequencer;
                registerAppChainFunction.SequencerSignature = sequencerSignature;
            
             return ContractHandler.SendRequestAsync(registerAppChainFunction);
        }

        public virtual Task<TransactionReceipt> RegisterAppChainRequestAndWaitForReceiptAsync(ulong chainId, string sequencer, byte[] sequencerSignature, CancellationTokenSource cancellationToken = null)
        {
            var registerAppChainFunction = new RegisterAppChainFunction();
                registerAppChainFunction.ChainId = chainId;
                registerAppChainFunction.Sequencer = sequencer;
                registerAppChainFunction.SequencerSignature = sequencerSignature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerAppChainFunction, cancellationToken);
        }

        public Task<BigInteger> RegistrationFeeQueryAsync(RegistrationFeeFunction registrationFeeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrationFeeFunction, BigInteger>(registrationFeeFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> RegistrationFeeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrationFeeFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> SendMessageRequestAsync(SendMessageFunction sendMessageFunction)
        {
             return ContractHandler.SendRequestAsync(sendMessageFunction);
        }

        public virtual Task<TransactionReceipt> SendMessageRequestAndWaitForReceiptAsync(SendMessageFunction sendMessageFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(sendMessageFunction, cancellationToken);
        }

        public virtual Task<string> SendMessageRequestAsync(ulong sourceChainId, ulong targetChainId, string target, byte[] data)
        {
            var sendMessageFunction = new SendMessageFunction();
                sendMessageFunction.SourceChainId = sourceChainId;
                sendMessageFunction.TargetChainId = targetChainId;
                sendMessageFunction.Target = target;
                sendMessageFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(sendMessageFunction);
        }

        public virtual Task<TransactionReceipt> SendMessageRequestAndWaitForReceiptAsync(ulong sourceChainId, ulong targetChainId, string target, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var sendMessageFunction = new SendMessageFunction();
                sendMessageFunction.SourceChainId = sourceChainId;
                sendMessageFunction.TargetChainId = targetChainId;
                sendMessageFunction.Target = target;
                sendMessageFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(sendMessageFunction, cancellationToken);
        }

        public virtual Task<string> SetAuthorizedSenderRequestAsync(SetAuthorizedSenderFunction setAuthorizedSenderFunction)
        {
             return ContractHandler.SendRequestAsync(setAuthorizedSenderFunction);
        }

        public virtual Task<TransactionReceipt> SetAuthorizedSenderRequestAndWaitForReceiptAsync(SetAuthorizedSenderFunction setAuthorizedSenderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAuthorizedSenderFunction, cancellationToken);
        }

        public virtual Task<string> SetAuthorizedSenderRequestAsync(ulong chainId, string sender, bool authorized)
        {
            var setAuthorizedSenderFunction = new SetAuthorizedSenderFunction();
                setAuthorizedSenderFunction.ChainId = chainId;
                setAuthorizedSenderFunction.Sender = sender;
                setAuthorizedSenderFunction.Authorized = authorized;
            
             return ContractHandler.SendRequestAsync(setAuthorizedSenderFunction);
        }

        public virtual Task<TransactionReceipt> SetAuthorizedSenderRequestAndWaitForReceiptAsync(ulong chainId, string sender, bool authorized, CancellationTokenSource cancellationToken = null)
        {
            var setAuthorizedSenderFunction = new SetAuthorizedSenderFunction();
                setAuthorizedSenderFunction.ChainId = chainId;
                setAuthorizedSenderFunction.Sender = sender;
                setAuthorizedSenderFunction.Authorized = authorized;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAuthorizedSenderFunction, cancellationToken);
        }

        public virtual Task<string> SetHubFeeBpsRequestAsync(SetHubFeeBpsFunction setHubFeeBpsFunction)
        {
             return ContractHandler.SendRequestAsync(setHubFeeBpsFunction);
        }

        public virtual Task<TransactionReceipt> SetHubFeeBpsRequestAndWaitForReceiptAsync(SetHubFeeBpsFunction setHubFeeBpsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setHubFeeBpsFunction, cancellationToken);
        }

        public virtual Task<string> SetHubFeeBpsRequestAsync(BigInteger newBps)
        {
            var setHubFeeBpsFunction = new SetHubFeeBpsFunction();
                setHubFeeBpsFunction.NewBps = newBps;
            
             return ContractHandler.SendRequestAsync(setHubFeeBpsFunction);
        }

        public virtual Task<TransactionReceipt> SetHubFeeBpsRequestAndWaitForReceiptAsync(BigInteger newBps, CancellationTokenSource cancellationToken = null)
        {
            var setHubFeeBpsFunction = new SetHubFeeBpsFunction();
                setHubFeeBpsFunction.NewBps = newBps;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setHubFeeBpsFunction, cancellationToken);
        }

        public virtual Task<string> SetMessageFeeRequestAsync(SetMessageFeeFunction setMessageFeeFunction)
        {
             return ContractHandler.SendRequestAsync(setMessageFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetMessageFeeRequestAndWaitForReceiptAsync(SetMessageFeeFunction setMessageFeeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMessageFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetMessageFeeRequestAsync(BigInteger newFee)
        {
            var setMessageFeeFunction = new SetMessageFeeFunction();
                setMessageFeeFunction.NewFee = newFee;
            
             return ContractHandler.SendRequestAsync(setMessageFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetMessageFeeRequestAndWaitForReceiptAsync(BigInteger newFee, CancellationTokenSource cancellationToken = null)
        {
            var setMessageFeeFunction = new SetMessageFeeFunction();
                setMessageFeeFunction.NewFee = newFee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMessageFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetRegistrationFeeRequestAsync(SetRegistrationFeeFunction setRegistrationFeeFunction)
        {
             return ContractHandler.SendRequestAsync(setRegistrationFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetRegistrationFeeRequestAndWaitForReceiptAsync(SetRegistrationFeeFunction setRegistrationFeeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistrationFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetRegistrationFeeRequestAsync(BigInteger newFee)
        {
            var setRegistrationFeeFunction = new SetRegistrationFeeFunction();
                setRegistrationFeeFunction.NewFee = newFee;
            
             return ContractHandler.SendRequestAsync(setRegistrationFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetRegistrationFeeRequestAndWaitForReceiptAsync(BigInteger newFee, CancellationTokenSource cancellationToken = null)
        {
            var setRegistrationFeeFunction = new SetRegistrationFeeFunction();
                setRegistrationFeeFunction.NewFee = newFee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistrationFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetSequencerRequestAsync(SetSequencerFunction setSequencerFunction)
        {
             return ContractHandler.SendRequestAsync(setSequencerFunction);
        }

        public virtual Task<TransactionReceipt> SetSequencerRequestAndWaitForReceiptAsync(SetSequencerFunction setSequencerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSequencerFunction, cancellationToken);
        }

        public virtual Task<string> SetSequencerRequestAsync(ulong chainId, string newSequencer)
        {
            var setSequencerFunction = new SetSequencerFunction();
                setSequencerFunction.ChainId = chainId;
                setSequencerFunction.NewSequencer = newSequencer;
            
             return ContractHandler.SendRequestAsync(setSequencerFunction);
        }

        public virtual Task<TransactionReceipt> SetSequencerRequestAndWaitForReceiptAsync(ulong chainId, string newSequencer, CancellationTokenSource cancellationToken = null)
        {
            var setSequencerFunction = new SetSequencerFunction();
                setSequencerFunction.ChainId = chainId;
                setSequencerFunction.NewSequencer = newSequencer;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSequencerFunction, cancellationToken);
        }

        public virtual Task<string> SetVerifierRequestAsync(SetVerifierFunction setVerifierFunction)
        {
             return ContractHandler.SendRequestAsync(setVerifierFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifierRequestAndWaitForReceiptAsync(SetVerifierFunction setVerifierFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifierFunction, cancellationToken);
        }

        public virtual Task<string> SetVerifierRequestAsync(ulong chainId, string verifier)
        {
            var setVerifierFunction = new SetVerifierFunction();
                setVerifierFunction.ChainId = chainId;
                setVerifierFunction.Verifier = verifier;
            
             return ContractHandler.SendRequestAsync(setVerifierFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifierRequestAndWaitForReceiptAsync(ulong chainId, string verifier, CancellationTokenSource cancellationToken = null)
        {
            var setVerifierFunction = new SetVerifierFunction();
                setVerifierFunction.ChainId = chainId;
                setVerifierFunction.Verifier = verifier;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifierFunction, cancellationToken);
        }

        public virtual Task<string> TransferAppChainOwnershipRequestAsync(TransferAppChainOwnershipFunction transferAppChainOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferAppChainOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferAppChainOwnershipRequestAndWaitForReceiptAsync(TransferAppChainOwnershipFunction transferAppChainOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferAppChainOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferAppChainOwnershipRequestAsync(ulong chainId, string newOwner)
        {
            var transferAppChainOwnershipFunction = new TransferAppChainOwnershipFunction();
                transferAppChainOwnershipFunction.ChainId = chainId;
                transferAppChainOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferAppChainOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferAppChainOwnershipRequestAndWaitForReceiptAsync(ulong chainId, string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferAppChainOwnershipFunction = new TransferAppChainOwnershipFunction();
                transferAppChainOwnershipFunction.ChainId = chainId;
                transferAppChainOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferAppChainOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferHubOwnershipRequestAsync(TransferHubOwnershipFunction transferHubOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferHubOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferHubOwnershipRequestAndWaitForReceiptAsync(TransferHubOwnershipFunction transferHubOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferHubOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferHubOwnershipRequestAsync(string newOwner)
        {
            var transferHubOwnershipFunction = new TransferHubOwnershipFunction();
                transferHubOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferHubOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferHubOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferHubOwnershipFunction = new TransferHubOwnershipFunction();
                transferHubOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferHubOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> UpdateMetadataRequestAsync(UpdateMetadataFunction updateMetadataFunction)
        {
             return ContractHandler.SendRequestAsync(updateMetadataFunction);
        }

        public virtual Task<TransactionReceipt> UpdateMetadataRequestAndWaitForReceiptAsync(UpdateMetadataFunction updateMetadataFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateMetadataFunction, cancellationToken);
        }

        public virtual Task<string> UpdateMetadataRequestAsync(ulong chainId, string name, string description, string url)
        {
            var updateMetadataFunction = new UpdateMetadataFunction();
                updateMetadataFunction.ChainId = chainId;
                updateMetadataFunction.Name = name;
                updateMetadataFunction.Description = description;
                updateMetadataFunction.Url = url;
            
             return ContractHandler.SendRequestAsync(updateMetadataFunction);
        }

        public virtual Task<TransactionReceipt> UpdateMetadataRequestAndWaitForReceiptAsync(ulong chainId, string name, string description, string url, CancellationTokenSource cancellationToken = null)
        {
            var updateMetadataFunction = new UpdateMetadataFunction();
                updateMetadataFunction.ChainId = chainId;
                updateMetadataFunction.Name = name;
                updateMetadataFunction.Description = description;
                updateMetadataFunction.Url = url;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateMetadataFunction, cancellationToken);
        }

        public Task<string> VerifiersQueryAsync(VerifiersFunction verifiersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifiersFunction, string>(verifiersFunction, blockParameter);
        }

        
        public virtual Task<string> VerifiersQueryAsync(ulong returnValue1, BlockParameter blockParameter = null)
        {
            var verifiersFunction = new VerifiersFunction();
                verifiersFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<VerifiersFunction, string>(verifiersFunction, blockParameter);
        }

        public Task<bool> VerifyAnchorQueryAsync(VerifyAnchorFunction verifyAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyAnchorFunction, bool>(verifyAnchorFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyAnchorQueryAsync(ulong chainId, ulong blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot, BlockParameter blockParameter = null)
        {
            var verifyAnchorFunction = new VerifyAnchorFunction();
                verifyAnchorFunction.ChainId = chainId;
                verifyAnchorFunction.BlockNumber = blockNumber;
                verifyAnchorFunction.StateRoot = stateRoot;
                verifyAnchorFunction.TxRoot = txRoot;
                verifyAnchorFunction.ReceiptRoot = receiptRoot;
            
            return ContractHandler.QueryAsync<VerifyAnchorFunction, bool>(verifyAnchorFunction, blockParameter);
        }

        public Task<bool> VerifyAnchorProofQueryAsync(VerifyAnchorProofFunction verifyAnchorProofFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyAnchorProofFunction, bool>(verifyAnchorProofFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyAnchorProofQueryAsync(ulong chainId, ulong blockNumber, byte[] proof, BlockParameter blockParameter = null)
        {
            var verifyAnchorProofFunction = new VerifyAnchorProofFunction();
                verifyAnchorProofFunction.ChainId = chainId;
                verifyAnchorProofFunction.BlockNumber = blockNumber;
                verifyAnchorProofFunction.Proof = proof;
            
            return ContractHandler.QueryAsync<VerifyAnchorProofFunction, bool>(verifyAnchorProofFunction, blockParameter);
        }

        public Task<bool> VerifyMessageInclusionQueryAsync(VerifyMessageInclusionFunction verifyMessageInclusionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyMessageInclusionFunction, bool>(verifyMessageInclusionFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyMessageInclusionQueryAsync(ulong chainId, List<byte[]> proof, ulong sourceChainId, ulong messageId, byte[] txHash, bool success, byte[] dataHash, BlockParameter blockParameter = null)
        {
            var verifyMessageInclusionFunction = new VerifyMessageInclusionFunction();
                verifyMessageInclusionFunction.ChainId = chainId;
                verifyMessageInclusionFunction.Proof = proof;
                verifyMessageInclusionFunction.SourceChainId = sourceChainId;
                verifyMessageInclusionFunction.MessageId = messageId;
                verifyMessageInclusionFunction.TxHash = txHash;
                verifyMessageInclusionFunction.Success = success;
                verifyMessageInclusionFunction.DataHash = dataHash;
            
            return ContractHandler.QueryAsync<VerifyMessageInclusionFunction, bool>(verifyMessageInclusionFunction, blockParameter);
        }

        public virtual Task<string> WithdrawFeesRequestAsync(WithdrawFeesFunction withdrawFeesFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFeesFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawFeesRequestAndWaitForReceiptAsync(WithdrawFeesFunction withdrawFeesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFeesFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawFeesRequestAsync(ulong chainId)
        {
            var withdrawFeesFunction = new WithdrawFeesFunction();
                withdrawFeesFunction.ChainId = chainId;
            
             return ContractHandler.SendRequestAsync(withdrawFeesFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawFeesRequestAndWaitForReceiptAsync(ulong chainId, CancellationTokenSource cancellationToken = null)
        {
            var withdrawFeesFunction = new WithdrawFeesFunction();
                withdrawFeesFunction.ChainId = chainId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFeesFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawHubFeesRequestAsync(WithdrawHubFeesFunction withdrawHubFeesFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawHubFeesFunction);
        }

        public virtual Task<string> WithdrawHubFeesRequestAsync()
        {
             return ContractHandler.SendRequestAsync<WithdrawHubFeesFunction>();
        }

        public virtual Task<TransactionReceipt> WithdrawHubFeesRequestAndWaitForReceiptAsync(WithdrawHubFeesFunction withdrawHubFeesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawHubFeesFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> WithdrawHubFeesRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<WithdrawHubFeesFunction>(null, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MaxChainIdFunction),
                typeof(MaxMessageSizeFunction),
                typeof(AcknowledgeMessagesFunction),
                typeof(AnchorFunction),
                typeof(AnchorsFunction),
                typeof(AppChainsFunction),
                typeof(AuthorizedSendersFunction),
                typeof(GetAnchorFunction),
                typeof(GetAppChainInfoFunction),
                typeof(GetMessageFunction),
                typeof(GetMessageRangeFunction),
                typeof(GetMessageRootCheckpointFunction),
                typeof(HubBalanceFunction),
                typeof(HubFeeBpsFunction),
                typeof(HubOwnerFunction),
                typeof(MessageFeeFunction),
                typeof(MessageRootCheckpointsFunction),
                typeof(MessagesFunction),
                typeof(OwnerBalancesFunction),
                typeof(PendingMessageCountFunction),
                typeof(RegisterAppChainFunction),
                typeof(RegistrationFeeFunction),
                typeof(SendMessageFunction),
                typeof(SetAuthorizedSenderFunction),
                typeof(SetHubFeeBpsFunction),
                typeof(SetMessageFeeFunction),
                typeof(SetRegistrationFeeFunction),
                typeof(SetSequencerFunction),
                typeof(SetVerifierFunction),
                typeof(TransferAppChainOwnershipFunction),
                typeof(TransferHubOwnershipFunction),
                typeof(UpdateMetadataFunction),
                typeof(VerifiersFunction),
                typeof(VerifyAnchorFunction),
                typeof(VerifyAnchorProofFunction),
                typeof(VerifyMessageInclusionFunction),
                typeof(WithdrawFeesFunction),
                typeof(WithdrawHubFeesFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AnchoredEventDTO),
                typeof(AppChainMetadataUpdatedEventDTO),
                typeof(AppChainRegisteredEventDTO),
                typeof(AuthorizedSenderChangedEventDTO),
                typeof(HubFeeBpsChangedEventDTO),
                typeof(HubOwnerChangedEventDTO),
                typeof(MessageFeeChangedEventDTO),
                typeof(MessageSentEventDTO),
                typeof(MessagesAcknowledgedEventDTO),
                typeof(RegistrationFeeChangedEventDTO),
                typeof(SequencerChangedEventDTO),
                typeof(VerifierChangedEventDTO)
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
