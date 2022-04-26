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
using Nethereum.Optimism.L1StandardBridge.ContractDefinition;

namespace Nethereum.Optimism.L1StandardBridge
{
    public partial class L1StandardBridgeService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, L1StandardBridgeDeployment l1StandardBridgeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<L1StandardBridgeDeployment>().SendRequestAndWaitForReceiptAsync(l1StandardBridgeDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, L1StandardBridgeDeployment l1StandardBridgeDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<L1StandardBridgeDeployment>().SendRequestAsync(l1StandardBridgeDeployment);
        }

        public static async Task<L1StandardBridgeService> DeployContractAndGetServiceAsync(Web3.Web3 web3, L1StandardBridgeDeployment l1StandardBridgeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, l1StandardBridgeDeployment, cancellationTokenSource);
            return new L1StandardBridgeService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public L1StandardBridgeService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> DepositERC20RequestAsync(DepositERC20Function depositERC20Function)
        {
            return ContractHandler.SendRequestAsync(depositERC20Function);
        }

        public Task<TransactionReceipt> DepositERC20RequestAndWaitForReceiptAsync(DepositERC20Function depositERC20Function, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositERC20Function, cancellationToken);
        }

        public Task<string> DepositERC20RequestAsync(string l1Token, string l2Token, BigInteger amount, uint l2Gas, byte[] data)
        {
            var depositERC20Function = new DepositERC20Function();
            depositERC20Function.L1Token = l1Token;
            depositERC20Function.L2Token = l2Token;
            depositERC20Function.Amount = amount;
            depositERC20Function.L2Gas = l2Gas;
            depositERC20Function.Data = data;

            return ContractHandler.SendRequestAsync(depositERC20Function);
        }

        public Task<TransactionReceipt> DepositERC20RequestAndWaitForReceiptAsync(string l1Token, string l2Token, BigInteger amount, uint l2Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var depositERC20Function = new DepositERC20Function();
            depositERC20Function.L1Token = l1Token;
            depositERC20Function.L2Token = l2Token;
            depositERC20Function.Amount = amount;
            depositERC20Function.L2Gas = l2Gas;
            depositERC20Function.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositERC20Function, cancellationToken);
        }

        public Task<string> DepositERC20ToRequestAsync(DepositERC20ToFunction depositERC20ToFunction)
        {
            return ContractHandler.SendRequestAsync(depositERC20ToFunction);
        }

        public Task<TransactionReceipt> DepositERC20ToRequestAndWaitForReceiptAsync(DepositERC20ToFunction depositERC20ToFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositERC20ToFunction, cancellationToken);
        }

        public Task<string> DepositERC20ToRequestAsync(string l1Token, string l2Token, string to, BigInteger amount, uint l2Gas, byte[] data)
        {
            var depositERC20ToFunction = new DepositERC20ToFunction();
            depositERC20ToFunction.L1Token = l1Token;
            depositERC20ToFunction.L2Token = l2Token;
            depositERC20ToFunction.To = to;
            depositERC20ToFunction.Amount = amount;
            depositERC20ToFunction.L2Gas = l2Gas;
            depositERC20ToFunction.Data = data;

            return ContractHandler.SendRequestAsync(depositERC20ToFunction);
        }

        public Task<TransactionReceipt> DepositERC20ToRequestAndWaitForReceiptAsync(string l1Token, string l2Token, string to, BigInteger amount, uint l2Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var depositERC20ToFunction = new DepositERC20ToFunction();
            depositERC20ToFunction.L1Token = l1Token;
            depositERC20ToFunction.L2Token = l2Token;
            depositERC20ToFunction.To = to;
            depositERC20ToFunction.Amount = amount;
            depositERC20ToFunction.L2Gas = l2Gas;
            depositERC20ToFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositERC20ToFunction, cancellationToken);
        }

        public Task<string> DepositETHRequestAsync(DepositETHFunction depositETHFunction)
        {
            return ContractHandler.SendRequestAsync(depositETHFunction);
        }

        public Task<TransactionReceipt> DepositETHRequestAndWaitForReceiptAsync(DepositETHFunction depositETHFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositETHFunction, cancellationToken);
        }

        public Task<string> DepositETHRequestAsync(uint l2Gas, byte[] data)
        {
            var depositETHFunction = new DepositETHFunction();
            depositETHFunction.L2Gas = l2Gas;
            depositETHFunction.Data = data;

            return ContractHandler.SendRequestAsync(depositETHFunction);
        }

        public Task<TransactionReceipt> DepositETHRequestAndWaitForReceiptAsync(uint l2Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var depositETHFunction = new DepositETHFunction();
            depositETHFunction.L2Gas = l2Gas;
            depositETHFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositETHFunction, cancellationToken);
        }

        public Task<string> DepositETHToRequestAsync(DepositETHToFunction depositETHToFunction)
        {
            return ContractHandler.SendRequestAsync(depositETHToFunction);
        }

        public Task<TransactionReceipt> DepositETHToRequestAndWaitForReceiptAsync(DepositETHToFunction depositETHToFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositETHToFunction, cancellationToken);
        }

        public Task<string> DepositETHToRequestAsync(string to, uint l2Gas, byte[] data)
        {
            var depositETHToFunction = new DepositETHToFunction();
            depositETHToFunction.To = to;
            depositETHToFunction.L2Gas = l2Gas;
            depositETHToFunction.Data = data;

            return ContractHandler.SendRequestAsync(depositETHToFunction);
        }

        public Task<TransactionReceipt> DepositETHToRequestAndWaitForReceiptAsync(string to, uint l2Gas, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var depositETHToFunction = new DepositETHToFunction();
            depositETHToFunction.To = to;
            depositETHToFunction.L2Gas = l2Gas;
            depositETHToFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(depositETHToFunction, cancellationToken);
        }

        public Task<BigInteger> DepositsQueryAsync(DepositsFunction depositsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DepositsFunction, BigInteger>(depositsFunction, blockParameter);
        }


        public Task<BigInteger> DepositsQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var depositsFunction = new DepositsFunction();
            depositsFunction.ReturnValue1 = returnValue1;
            depositsFunction.ReturnValue2 = returnValue2;

            return ContractHandler.QueryAsync<DepositsFunction, BigInteger>(depositsFunction, blockParameter);
        }

        public Task<string> DonateETHRequestAsync(DonateETHFunction donateETHFunction)
        {
            return ContractHandler.SendRequestAsync(donateETHFunction);
        }

        public Task<string> DonateETHRequestAsync()
        {
            return ContractHandler.SendRequestAsync<DonateETHFunction>();
        }

        public Task<TransactionReceipt> DonateETHRequestAndWaitForReceiptAsync(DonateETHFunction donateETHFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(donateETHFunction, cancellationToken);
        }

        public Task<TransactionReceipt> DonateETHRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<DonateETHFunction>(null, cancellationToken);
        }

        public Task<string> FinalizeERC20WithdrawalRequestAsync(FinalizeERC20WithdrawalFunction finalizeERC20WithdrawalFunction)
        {
            return ContractHandler.SendRequestAsync(finalizeERC20WithdrawalFunction);
        }

        public Task<TransactionReceipt> FinalizeERC20WithdrawalRequestAndWaitForReceiptAsync(FinalizeERC20WithdrawalFunction finalizeERC20WithdrawalFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeERC20WithdrawalFunction, cancellationToken);
        }

        public Task<string> FinalizeERC20WithdrawalRequestAsync(string l1Token, string l2Token, string from, string to, BigInteger amount, byte[] data)
        {
            var finalizeERC20WithdrawalFunction = new FinalizeERC20WithdrawalFunction();
            finalizeERC20WithdrawalFunction.L1Token = l1Token;
            finalizeERC20WithdrawalFunction.L2Token = l2Token;
            finalizeERC20WithdrawalFunction.From = from;
            finalizeERC20WithdrawalFunction.To = to;
            finalizeERC20WithdrawalFunction.Amount = amount;
            finalizeERC20WithdrawalFunction.Data = data;

            return ContractHandler.SendRequestAsync(finalizeERC20WithdrawalFunction);
        }

        public Task<TransactionReceipt> FinalizeERC20WithdrawalRequestAndWaitForReceiptAsync(string l1Token, string l2Token, string from, string to, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var finalizeERC20WithdrawalFunction = new FinalizeERC20WithdrawalFunction();
            finalizeERC20WithdrawalFunction.L1Token = l1Token;
            finalizeERC20WithdrawalFunction.L2Token = l2Token;
            finalizeERC20WithdrawalFunction.From = from;
            finalizeERC20WithdrawalFunction.To = to;
            finalizeERC20WithdrawalFunction.Amount = amount;
            finalizeERC20WithdrawalFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeERC20WithdrawalFunction, cancellationToken);
        }

        public Task<string> FinalizeETHWithdrawalRequestAsync(FinalizeETHWithdrawalFunction finalizeETHWithdrawalFunction)
        {
            return ContractHandler.SendRequestAsync(finalizeETHWithdrawalFunction);
        }

        public Task<TransactionReceipt> FinalizeETHWithdrawalRequestAndWaitForReceiptAsync(FinalizeETHWithdrawalFunction finalizeETHWithdrawalFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeETHWithdrawalFunction, cancellationToken);
        }

        public Task<string> FinalizeETHWithdrawalRequestAsync(string from, string to, BigInteger amount, byte[] data)
        {
            var finalizeETHWithdrawalFunction = new FinalizeETHWithdrawalFunction();
            finalizeETHWithdrawalFunction.From = from;
            finalizeETHWithdrawalFunction.To = to;
            finalizeETHWithdrawalFunction.Amount = amount;
            finalizeETHWithdrawalFunction.Data = data;

            return ContractHandler.SendRequestAsync(finalizeETHWithdrawalFunction);
        }

        public Task<TransactionReceipt> FinalizeETHWithdrawalRequestAndWaitForReceiptAsync(string from, string to, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var finalizeETHWithdrawalFunction = new FinalizeETHWithdrawalFunction();
            finalizeETHWithdrawalFunction.From = from;
            finalizeETHWithdrawalFunction.To = to;
            finalizeETHWithdrawalFunction.Amount = amount;
            finalizeETHWithdrawalFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeETHWithdrawalFunction, cancellationToken);
        }

        public Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
            return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> InitializeRequestAsync(string l1messenger, string l2TokenBridge)
        {
            var initializeFunction = new InitializeFunction();
            initializeFunction.L1messenger = l1messenger;
            initializeFunction.L2TokenBridge = l2TokenBridge;

            return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(string l1messenger, string l2TokenBridge, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
            initializeFunction.L1messenger = l1messenger;
            initializeFunction.L2TokenBridge = l2TokenBridge;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> L2TokenBridgeQueryAsync(L2TokenBridgeFunction l2TokenBridgeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L2TokenBridgeFunction, string>(l2TokenBridgeFunction, blockParameter);
        }


        public Task<string> L2TokenBridgeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L2TokenBridgeFunction, string>(null, blockParameter);
        }

        public Task<string> MessengerQueryAsync(MessengerFunction messengerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessengerFunction, string>(messengerFunction, blockParameter);
        }


        public Task<string> MessengerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MessengerFunction, string>(null, blockParameter);
        }
    }
}
