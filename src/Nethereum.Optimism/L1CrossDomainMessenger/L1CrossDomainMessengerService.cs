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
using Nethereum.Optimism.L1CrossDomainMessenger.ContractDefinition;

namespace Nethereum.Optimism.L1CrossDomainMessenger
{
    public partial class L1CrossDomainMessengerService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, L1CrossDomainMessengerDeployment l1CrossDomainMessengerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<L1CrossDomainMessengerDeployment>().SendRequestAndWaitForReceiptAsync(l1CrossDomainMessengerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, L1CrossDomainMessengerDeployment l1CrossDomainMessengerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<L1CrossDomainMessengerDeployment>().SendRequestAsync(l1CrossDomainMessengerDeployment);
        }

        public static async Task<L1CrossDomainMessengerService> DeployContractAndGetServiceAsync(Web3.Web3 web3, L1CrossDomainMessengerDeployment l1CrossDomainMessengerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, l1CrossDomainMessengerDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new L1CrossDomainMessengerService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public L1CrossDomainMessengerService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> AllowMessageRequestAsync(AllowMessageFunction allowMessageFunction)
        {
            return ContractHandler.SendRequestAsync(allowMessageFunction);
        }

        public Task<TransactionReceipt> AllowMessageRequestAndWaitForReceiptAsync(AllowMessageFunction allowMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(allowMessageFunction, cancellationToken);
        }

        public Task<string> AllowMessageRequestAsync(byte[] xDomainCalldataHash)
        {
            var allowMessageFunction = new AllowMessageFunction();
            allowMessageFunction.XDomainCalldataHash = xDomainCalldataHash;

            return ContractHandler.SendRequestAsync(allowMessageFunction);
        }

        public Task<TransactionReceipt> AllowMessageRequestAndWaitForReceiptAsync(byte[] xDomainCalldataHash, CancellationTokenSource cancellationToken = null)
        {
            var allowMessageFunction = new AllowMessageFunction();
            allowMessageFunction.XDomainCalldataHash = xDomainCalldataHash;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(allowMessageFunction, cancellationToken);
        }

        public Task<string> BlockMessageRequestAsync(BlockMessageFunction blockMessageFunction)
        {
            return ContractHandler.SendRequestAsync(blockMessageFunction);
        }

        public Task<TransactionReceipt> BlockMessageRequestAndWaitForReceiptAsync(BlockMessageFunction blockMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(blockMessageFunction, cancellationToken);
        }

        public Task<string> BlockMessageRequestAsync(byte[] xDomainCalldataHash)
        {
            var blockMessageFunction = new BlockMessageFunction();
            blockMessageFunction.XDomainCalldataHash = xDomainCalldataHash;

            return ContractHandler.SendRequestAsync(blockMessageFunction);
        }

        public Task<TransactionReceipt> BlockMessageRequestAndWaitForReceiptAsync(byte[] xDomainCalldataHash, CancellationTokenSource cancellationToken = null)
        {
            var blockMessageFunction = new BlockMessageFunction();
            blockMessageFunction.XDomainCalldataHash = xDomainCalldataHash;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(blockMessageFunction, cancellationToken);
        }

        public Task<bool> BlockedMessagesQueryAsync(BlockedMessagesFunction blockedMessagesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BlockedMessagesFunction, bool>(blockedMessagesFunction, blockParameter);
        }


        public Task<bool> BlockedMessagesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var blockedMessagesFunction = new BlockedMessagesFunction();
            blockedMessagesFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<BlockedMessagesFunction, bool>(blockedMessagesFunction, blockParameter);
        }

        public Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
            return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> InitializeRequestAsync(string libAddressManager)
        {
            var initializeFunction = new InitializeFunction();
            initializeFunction.LibAddressManager = libAddressManager;

            return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(string libAddressManager, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
            initializeFunction.LibAddressManager = libAddressManager;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> LibAddressManagerQueryAsync(LibAddressManagerFunction libAddressManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LibAddressManagerFunction, string>(libAddressManagerFunction, blockParameter);
        }


        public Task<string> LibAddressManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LibAddressManagerFunction, string>(null, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }


        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> PauseRequestAsync(PauseFunction pauseFunction)
        {
            return ContractHandler.SendRequestAsync(pauseFunction);
        }

        public Task<string> PauseRequestAsync()
        {
            return ContractHandler.SendRequestAsync<PauseFunction>();
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(PauseFunction pauseFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(pauseFunction, cancellationToken);
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<PauseFunction>(null, cancellationToken);
        }

        public Task<bool> PausedQueryAsync(PausedFunction pausedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(pausedFunction, blockParameter);
        }


        public Task<bool> PausedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(null, blockParameter);
        }

        public Task<string> RelayMessageRequestAsync(RelayMessageFunction relayMessageFunction)
        {
            return ContractHandler.SendRequestAsync(relayMessageFunction);
        }

        public Task<TransactionReceipt> RelayMessageRequestAndWaitForReceiptAsync(RelayMessageFunction relayMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(relayMessageFunction, cancellationToken);
        }

        public Task<string> RelayMessageRequestAsync(string target, string sender, byte[] message, BigInteger messageNonce, L2MessageInclusionProof proof)
        {
            var relayMessageFunction = new RelayMessageFunction();
            relayMessageFunction.Target = target;
            relayMessageFunction.Sender = sender;
            relayMessageFunction.Message = message;
            relayMessageFunction.MessageNonce = messageNonce;
            relayMessageFunction.Proof = proof;

            return ContractHandler.SendRequestAsync(relayMessageFunction);
        }

        public Task<TransactionReceipt> RelayMessageRequestAndWaitForReceiptAsync(string target, string sender, byte[] message, BigInteger messageNonce, L2MessageInclusionProof proof, CancellationTokenSource cancellationToken = null)
        {
            var relayMessageFunction = new RelayMessageFunction();
            relayMessageFunction.Target = target;
            relayMessageFunction.Sender = sender;
            relayMessageFunction.Message = message;
            relayMessageFunction.MessageNonce = messageNonce;
            relayMessageFunction.Proof = proof;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(relayMessageFunction, cancellationToken);
        }

        public Task<bool> RelayedMessagesQueryAsync(RelayedMessagesFunction relayedMessagesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RelayedMessagesFunction, bool>(relayedMessagesFunction, blockParameter);
        }


        public Task<bool> RelayedMessagesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var relayedMessagesFunction = new RelayedMessagesFunction();
            relayedMessagesFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<RelayedMessagesFunction, bool>(relayedMessagesFunction, blockParameter);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
            return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> ReplayMessageRequestAsync(ReplayMessageFunction replayMessageFunction)
        {
            return ContractHandler.SendRequestAsync(replayMessageFunction);
        }

        public Task<TransactionReceipt> ReplayMessageRequestAndWaitForReceiptAsync(ReplayMessageFunction replayMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(replayMessageFunction, cancellationToken);
        }

        public Task<string> ReplayMessageRequestAsync(string target, string sender, byte[] message, BigInteger queueIndex, uint oldGasLimit, uint newGasLimit)
        {
            var replayMessageFunction = new ReplayMessageFunction();
            replayMessageFunction.Target = target;
            replayMessageFunction.Sender = sender;
            replayMessageFunction.Message = message;
            replayMessageFunction.QueueIndex = queueIndex;
            replayMessageFunction.OldGasLimit = oldGasLimit;
            replayMessageFunction.NewGasLimit = newGasLimit;

            return ContractHandler.SendRequestAsync(replayMessageFunction);
        }

        public Task<TransactionReceipt> ReplayMessageRequestAndWaitForReceiptAsync(string target, string sender, byte[] message, BigInteger queueIndex, uint oldGasLimit, uint newGasLimit, CancellationTokenSource cancellationToken = null)
        {
            var replayMessageFunction = new ReplayMessageFunction();
            replayMessageFunction.Target = target;
            replayMessageFunction.Sender = sender;
            replayMessageFunction.Message = message;
            replayMessageFunction.QueueIndex = queueIndex;
            replayMessageFunction.OldGasLimit = oldGasLimit;
            replayMessageFunction.NewGasLimit = newGasLimit;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(replayMessageFunction, cancellationToken);
        }

        public Task<string> ResolveQueryAsync(ResolveFunction resolveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ResolveFunction, string>(resolveFunction, blockParameter);
        }


        public Task<string> ResolveQueryAsync(string name, BlockParameter blockParameter = null)
        {
            var resolveFunction = new ResolveFunction();
            resolveFunction.Name = name;

            return ContractHandler.QueryAsync<ResolveFunction, string>(resolveFunction, blockParameter);
        }

        public Task<string> SendMessageRequestAsync(SendMessageFunction sendMessageFunction)
        {
            return ContractHandler.SendRequestAsync(sendMessageFunction);
        }

        public Task<TransactionReceipt> SendMessageRequestAndWaitForReceiptAsync(SendMessageFunction sendMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(sendMessageFunction, cancellationToken);
        }

        public Task<string> SendMessageRequestAsync(string target, byte[] message, uint gasLimit)
        {
            var sendMessageFunction = new SendMessageFunction();
            sendMessageFunction.Target = target;
            sendMessageFunction.Message = message;
            sendMessageFunction.GasLimit = gasLimit;

            return ContractHandler.SendRequestAsync(sendMessageFunction);
        }

        public Task<TransactionReceipt> SendMessageRequestAndWaitForReceiptAsync(string target, byte[] message, uint gasLimit, CancellationTokenSource cancellationToken = null)
        {
            var sendMessageFunction = new SendMessageFunction();
            sendMessageFunction.Target = target;
            sendMessageFunction.Message = message;
            sendMessageFunction.GasLimit = gasLimit;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(sendMessageFunction, cancellationToken);
        }

        public Task<bool> SuccessfulMessagesQueryAsync(SuccessfulMessagesFunction successfulMessagesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SuccessfulMessagesFunction, bool>(successfulMessagesFunction, blockParameter);
        }


        public Task<bool> SuccessfulMessagesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var successfulMessagesFunction = new SuccessfulMessagesFunction();
            successfulMessagesFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<SuccessfulMessagesFunction, bool>(successfulMessagesFunction, blockParameter);
        }

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> XDomainMessageSenderQueryAsync(XDomainMessageSenderFunction xDomainMessageSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<XDomainMessageSenderFunction, string>(xDomainMessageSenderFunction, blockParameter);
        }


        public Task<string> XDomainMessageSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<XDomainMessageSenderFunction, string>(null, blockParameter);
        }
    }
}
