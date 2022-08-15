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
using Nethereum.Optimism.OVM_L2ToL1MessagePasser.ContractDefinition;

namespace Nethereum.Optimism.OVM_L2ToL1MessagePasser
{
    public partial class OVM_L2ToL1MessagePasserService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, OVM_L2ToL1MessagePasserDeployment oVM_L2ToL1MessagePasserDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<OVM_L2ToL1MessagePasserDeployment>().SendRequestAndWaitForReceiptAsync(oVM_L2ToL1MessagePasserDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, OVM_L2ToL1MessagePasserDeployment oVM_L2ToL1MessagePasserDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<OVM_L2ToL1MessagePasserDeployment>().SendRequestAsync(oVM_L2ToL1MessagePasserDeployment);
        }

        public static async Task<OVM_L2ToL1MessagePasserService> DeployContractAndGetServiceAsync(Web3.Web3 web3, OVM_L2ToL1MessagePasserDeployment oVM_L2ToL1MessagePasserDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, oVM_L2ToL1MessagePasserDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new OVM_L2ToL1MessagePasserService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public OVM_L2ToL1MessagePasserService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> PassMessageToL1RequestAsync(PassMessageToL1Function passMessageToL1Function)
        {
            return ContractHandler.SendRequestAsync(passMessageToL1Function);
        }

        public Task<TransactionReceipt> PassMessageToL1RequestAndWaitForReceiptAsync(PassMessageToL1Function passMessageToL1Function, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(passMessageToL1Function, cancellationToken);
        }

        public Task<string> PassMessageToL1RequestAsync(byte[] message)
        {
            var passMessageToL1Function = new PassMessageToL1Function();
            passMessageToL1Function.Message = message;

            return ContractHandler.SendRequestAsync(passMessageToL1Function);
        }

        public Task<TransactionReceipt> PassMessageToL1RequestAndWaitForReceiptAsync(byte[] message, CancellationTokenSource cancellationToken = null)
        {
            var passMessageToL1Function = new PassMessageToL1Function();
            passMessageToL1Function.Message = message;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(passMessageToL1Function, cancellationToken);
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
    }
}
