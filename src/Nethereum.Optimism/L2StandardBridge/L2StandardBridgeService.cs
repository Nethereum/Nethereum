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
using Nethereum.Optimism.L2StandardBridge.ContractDefinition;

namespace Nethereum.Optimism.L2StandardBridge
{
    public partial class L2StandardBridgeService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, L2StandardBridgeDeployment l2StandardBridgeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<L2StandardBridgeDeployment>().SendRequestAndWaitForReceiptAsync(l2StandardBridgeDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, L2StandardBridgeDeployment l2StandardBridgeDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<L2StandardBridgeDeployment>().SendRequestAsync(l2StandardBridgeDeployment);
        }

        public static async Task<L2StandardBridgeService> DeployContractAndGetServiceAsync(Web3.Web3 web3, L2StandardBridgeDeployment l2StandardBridgeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, l2StandardBridgeDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new L2StandardBridgeService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public L2StandardBridgeService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> FinalizeDepositRequestAsync(FinalizeDepositFunction finalizeDepositFunction)
        {
            return ContractHandler.SendRequestAsync(finalizeDepositFunction);
        }

        public Task<TransactionReceipt> FinalizeDepositRequestAndWaitForReceiptAsync(FinalizeDepositFunction finalizeDepositFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeDepositFunction, cancellationToken);
        }

        public Task<string> FinalizeDepositRequestAsync(string l1Token, string l2Token, string from, string to, BigInteger amount, byte[] data)
        {
            var finalizeDepositFunction = new FinalizeDepositFunction();
            finalizeDepositFunction.L1Token = l1Token;
            finalizeDepositFunction.L2Token = l2Token;
            finalizeDepositFunction.From = from;
            finalizeDepositFunction.To = to;
            finalizeDepositFunction.Amount = amount;
            finalizeDepositFunction.Data = data;

            return ContractHandler.SendRequestAsync(finalizeDepositFunction);
        }

        public Task<TransactionReceipt> FinalizeDepositRequestAndWaitForReceiptAsync(string l1Token, string l2Token, string from, string to, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var finalizeDepositFunction = new FinalizeDepositFunction();
            finalizeDepositFunction.L1Token = l1Token;
            finalizeDepositFunction.L2Token = l2Token;
            finalizeDepositFunction.From = from;
            finalizeDepositFunction.To = to;
            finalizeDepositFunction.Amount = amount;
            finalizeDepositFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeDepositFunction, cancellationToken);
        }

        public Task<string> L1TokenBridgeQueryAsync(L1TokenBridgeFunction l1TokenBridgeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1TokenBridgeFunction, string>(l1TokenBridgeFunction, blockParameter);
        }


        public Task<string> L1TokenBridgeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1TokenBridgeFunction, string>(null, blockParameter);
        }

        public Task<string> MessengerQueryAsync(MessengerFunction messengerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessengerFunction, string>(messengerFunction, blockParameter);
        }


        public Task<string> MessengerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessengerFunction, string>(null, blockParameter);
        }

        public Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
            return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public Task<string> WithdrawRequestAsync(string l2Token, BigInteger amount, uint l1Gas, byte[] data)
        {
            var withdrawFunction = new WithdrawFunction();
            withdrawFunction.L2Token = l2Token;
            withdrawFunction.Amount = amount;
            withdrawFunction.L1Gas = l1Gas;
            withdrawFunction.Data = data;

            return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(string l2Token, BigInteger amount, uint l1Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var withdrawFunction = new WithdrawFunction();
            withdrawFunction.L2Token = l2Token;
            withdrawFunction.Amount = amount;
            withdrawFunction.L1Gas = l1Gas;
            withdrawFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public Task<string> WithdrawToRequestAsync(WithdrawToFunction withdrawToFunction)
        {
            return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(WithdrawToFunction withdrawToFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public Task<string> WithdrawToRequestAsync(string l2Token, string to, BigInteger amount, uint l1Gas, byte[] data)
        {
            var withdrawToFunction = new WithdrawToFunction();
            withdrawToFunction.L2Token = l2Token;
            withdrawToFunction.To = to;
            withdrawToFunction.Amount = amount;
            withdrawToFunction.L1Gas = l1Gas;
            withdrawToFunction.Data = data;

            return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(string l2Token, string to, BigInteger amount, uint l1Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var withdrawToFunction = new WithdrawToFunction();
            withdrawToFunction.L2Token = l2Token;
            withdrawToFunction.To = to;
            withdrawToFunction.Amount = amount;
            withdrawToFunction.L1Gas = l1Gas;
            withdrawToFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }
    }
}
