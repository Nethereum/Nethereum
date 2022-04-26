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
using Nethereum.Optimism.OVM_SequencerFeeVault.ContractDefinition;

namespace Nethereum.Optimism.OVM_SequencerFeeVault
{
    public partial class OVM_SequencerFeeVaultService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, OVM_SequencerFeeVaultDeployment oVM_SequencerFeeVaultDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<OVM_SequencerFeeVaultDeployment>().SendRequestAndWaitForReceiptAsync(oVM_SequencerFeeVaultDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, OVM_SequencerFeeVaultDeployment oVM_SequencerFeeVaultDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<OVM_SequencerFeeVaultDeployment>().SendRequestAsync(oVM_SequencerFeeVaultDeployment);
        }

        public static async Task<OVM_SequencerFeeVaultService> DeployContractAndGetServiceAsync(Web3.Web3 web3, OVM_SequencerFeeVaultDeployment oVM_SequencerFeeVaultDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, oVM_SequencerFeeVaultDeployment, cancellationTokenSource);
            return new OVM_SequencerFeeVaultService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public OVM_SequencerFeeVaultService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<BigInteger> MIN_WITHDRAWAL_AMOUNTQueryAsync(MIN_WITHDRAWAL_AMOUNTFunction mIN_WITHDRAWAL_AMOUNTFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MIN_WITHDRAWAL_AMOUNTFunction, BigInteger>(mIN_WITHDRAWAL_AMOUNTFunction, blockParameter);
        }


        public Task<BigInteger> MIN_WITHDRAWAL_AMOUNTQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MIN_WITHDRAWAL_AMOUNTFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> L1FeeWalletQueryAsync(L1FeeWalletFunction l1FeeWalletFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1FeeWalletFunction, string>(l1FeeWalletFunction, blockParameter);
        }


        public Task<string> L1FeeWalletQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<L1FeeWalletFunction, string>(null, blockParameter);
        }

        public Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
            return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public Task<string> WithdrawRequestAsync()
        {
            return ContractHandler.SendRequestAsync<WithdrawFunction>();
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<WithdrawFunction>(null, cancellationToken);
        }
    }
}
