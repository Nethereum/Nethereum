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
using Nethereum.Optimism.L2CrossDomainMessenger.ContractDefinition;

namespace Nethereum.Optimism.L2CrossDomainMessenger
{
    public partial class L2CrossDomainMessengerService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, L2CrossDomainMessengerDeployment l2CrossDomainMessengerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<L2CrossDomainMessengerDeployment>().SendRequestAndWaitForReceiptAsync(l2CrossDomainMessengerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, L2CrossDomainMessengerDeployment l2CrossDomainMessengerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<L2CrossDomainMessengerDeployment>().SendRequestAsync(l2CrossDomainMessengerDeployment);
        }

        public static async Task<L2CrossDomainMessengerService> DeployContractAndGetServiceAsync(Web3.Web3 web3, L2CrossDomainMessengerDeployment l2CrossDomainMessengerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, l2CrossDomainMessengerDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new L2CrossDomainMessengerService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public L2CrossDomainMessengerService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> L1CrossDomainMessengerQueryAsync(L1CrossDomainMessengerFunction l1CrossDomainMessengerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1CrossDomainMessengerFunction, string>(l1CrossDomainMessengerFunction, blockParameter);
        }


        public Task<string> L1CrossDomainMessengerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1CrossDomainMessengerFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MessageNonceQueryAsync(MessageNonceFunction messageNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessageNonceFunction, BigInteger>(messageNonceFunction, blockParameter);
        }


        public Task<BigInteger> MessageNonceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessageNonceFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> RelayMessageRequestAsync(RelayMessageFunction relayMessageFunction)
        {
            return ContractHandler.SendRequestAsync(relayMessageFunction);
        }

        public Task<TransactionReceipt> RelayMessageRequestAndWaitForReceiptAsync(RelayMessageFunction relayMessageFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(relayMessageFunction, cancellationToken);
        }

        public Task<string> RelayMessageRequestAsync(string target, string sender, byte[] message, BigInteger messageNonce)
        {
            var relayMessageFunction = new RelayMessageFunction();
            relayMessageFunction.Target = target;
            relayMessageFunction.Sender = sender;
            relayMessageFunction.Message = message;
            relayMessageFunction.MessageNonce = messageNonce;

            return ContractHandler.SendRequestAsync(relayMessageFunction);
        }

        public Task<TransactionReceipt> RelayMessageRequestAndWaitForReceiptAsync(string target, string sender, byte[] message, BigInteger messageNonce, CancellationTokenSource cancellationToken = null)
        {
            var relayMessageFunction = new RelayMessageFunction();
            relayMessageFunction.Target = target;
            relayMessageFunction.Sender = sender;
            relayMessageFunction.Message = message;
            relayMessageFunction.MessageNonce = messageNonce;

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

        public Task<bool> SentMessagesQueryAsync(SentMessagesFunction sentMessagesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SentMessagesFunction, bool>(sentMessagesFunction, blockParameter);
        }


        public Task<bool> SentMessagesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var sentMessagesFunction = new SentMessagesFunction();
            sentMessagesFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<SentMessagesFunction, bool>(sentMessagesFunction, blockParameter);
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
