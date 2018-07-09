using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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

        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20Deployment>().SendRequestAndWaitForReceiptAsync(eIP20Deployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20Deployment>().SendRequestAsync(eIP20Deployment);
        }
        public static async Task<StandardTokenService> DeployContractAndGetServiceAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eIP20Deployment, cancellationTokenSource);
            return new StandardTokenService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; set; }

        private string abi =
            @"[{""constant"":false,""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":""supply"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""from"",""type"":""address""},{""name"":""to"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""who"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""value"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""to"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""owner"",""type"":""address""},{""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":""_allowance"",""type"":""uint256""}],""type"":""function""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""from"",""type"":""address""},{""indexed"":true,""name"":""to"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""owner"",""type"":""address""},{""indexed"":true,""name"":""spender"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}]";

        protected Contract Contract { get; set; }

        public StandardTokenService(Web3.Web3 web3, string address)
        {
            this.Web3 = web3;
            this.Contract = web3.Eth.GetContract(abi, address);
            this.ContractHandler = web3.Eth.GetContractHandler(address);
        }

        protected ContractHandler ContractHandler { get; set; }

        public async Task<TNumber> GetTotalSupplyAsync<TNumber>()
        {
            var function = GetTotalSupplyFunction();
            return await function.CallAsync<TNumber>();
        }

        protected Function GetTotalSupplyFunction()
        {
            return Contract.GetFunction("totalSupply");
        }

        public async Task<T> GetBalanceOfAsync<T>(string address)
        {
            var function = GetBalanceOfFunction();
            return await function.CallAsync<T>(address);
        }

        protected Function GetBalanceOfFunction()
        {
            return Contract.GetFunction("balanceOf");
        }

        public async Task<T> GetAllowanceAsync<T>(string addressOwner, string addressSpender)
        {
            var function = GetAllowanceFunction();
            return await function.CallAsync<T>(addressOwner, addressSpender);
        }

        protected Function GetAllowanceFunction()
        {
            return Contract.GetFunction("allowance");
        }

        public async Task<string> TransferAsync<T>(string addressFrom, string addressTo, T value, HexBigInteger gas)
        {
            var function = GetTransferFunction();
           return await function.SendTransactionAsync(addressFrom, gas, null, addressTo, value);
        }

        public async Task<string> TransferAsync(TransferFunction transferMessage)
        {
            return await ContractHandler.SendRequestAsync(transferMessage).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> TransferAndWaitForReceiptAsync(TransferFunction transferMessage)
        {
            return await ContractHandler.SendRequestAndWaitForReceiptAsync(transferMessage).ConfigureAwait(false);
        }

        protected Function GetTransferFunction()
        {
            return Contract.GetFunction("transfer");
        }

        public async Task<string> TransferFromAsync<T>(string addressFrom, string addressTransferedFrom, string addressTransferedTo,
            T value, HexBigInteger gas)
        {
            var function = GetTransferFromFunction();
           return await function.SendTransactionAsync(addressFrom, gas, null, addressTransferedFrom, addressTransferedTo, value);
        }

        protected Function GetTransferFromFunction()
        {
            return Contract.GetFunction("transferFrom");
        }

        public async Task ApproveAsync<T>(string addressFrom, string addressSpender, T value, HexBigInteger gas = null)
        {
            var function = GetApproveFunction();
            await function.SendTransactionAsync(addressFrom, gas, null, addressSpender, value);
        }

        protected Function GetApproveFunction()
        {
            return Contract.GetFunction("approve");
        }

        public Event GetApprovalEvent()
        {
            return Contract.GetEvent("Approval");
        }

        public Event GetTransferEvent()
        {
            return Contract.GetEvent("Transfer");
        }

        public Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
            return ContractHandler.SendRequestAsync(approveFunction);
        }
        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }
        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
            return ContractHandler.SendRequestAsync(transferFromFunction);
        }
        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }
        public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(blockParameter);
        }

        public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }
        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        public Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(blockParameter);
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

    }
}
