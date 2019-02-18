using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.Decoders;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;

namespace Nethereum.StandardTokenEIP20
{
    public class StandardTokenService
    {

        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20Deployment>().SendRequestAndWaitForReceiptAsync(eIP20Deployment, token);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20Deployment>().SendRequestAsync(eIP20Deployment);
        }
        public static async Task<StandardTokenService> DeployContractAndGetServiceAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eIP20Deployment, token);
            return new StandardTokenService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; set; }

        public StandardTokenService(Web3.Web3 web3, string address)
        {
            this.Web3 = web3;
            this.ContractHandler = web3.Eth.GetContractHandler(address);
        }

        public ContractHandler ContractHandler { get; }

        public Event<ApprovalEventDTO> GetApprovalEvent()
        {
            return ContractHandler.GetEvent<ApprovalEventDTO>();
        }

        public Event<TransferEventDTO> GetTransferEvent()
        { 
            return ContractHandler.GetEvent<TransferEventDTO>();
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction = null, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryRawAsync<NameFunction, StringBytes32Decoder, string>(nameFunction, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction = null, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryRawAsync<SymbolFunction, StringBytes32Decoder, string>(symbolFunction, blockParameter);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
            return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationToken token = default(CancellationToken))
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, token);
        }

        public Task<string> ApproveRequestAsync(string spender, BigInteger value)
        {
            var approveFunction = new ApproveFunction();
            approveFunction.Spender = spender;
            approveFunction.Value = value;

            return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger value, CancellationToken token = default(CancellationToken))
        {
            var approveFunction = new ApproveFunction();
            approveFunction.Spender = spender;
            approveFunction.Value = value;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, token);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }


        public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
            return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationToken token = default(CancellationToken))
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, token);
        }

        public Task<string> TransferFromRequestAsync(string from, string to, BigInteger value)
        {
            var transferFromFunction = new TransferFromFunction();
            transferFromFunction.From = from;
            transferFromFunction.To = to;
            transferFromFunction.Value = value;

            return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, CancellationToken token = default(CancellationToken))
        {
            var transferFromFunction = new TransferFromFunction();
            transferFromFunction.From = from;
            transferFromFunction.To = to;
            transferFromFunction.Value = value;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, token);
        }

        public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }

        public Task<BigInteger> BalancesQueryAsync(string address, BlockParameter blockParameter = null)
        {
            var balancesFunction = new BalancesFunction();
            balancesFunction.Address = address;

            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(null, blockParameter);
        }

        public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }

        public Task<BigInteger> AllowedQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowedFunction = new AllowedFunction();
            allowedFunction.Owner = owner;
            allowedFunction.Spender = spender;

            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = owner;

            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
            return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationToken token = default(CancellationToken))
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, token);
        }

        public Task<string> TransferRequestAsync(string to, BigInteger value)
        {
            var transferFunction = new TransferFunction();
            transferFunction.To = to;
            transferFunction.Value = value;

            return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger value, CancellationToken token = default(CancellationToken))
        {
            var transferFunction = new TransferFunction();
            transferFunction.To = to;
            transferFunction.Value = value;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, token);
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        public Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
            allowanceFunction.Owner = owner;
            allowanceFunction.Spender = spender;

            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }
    }
}
