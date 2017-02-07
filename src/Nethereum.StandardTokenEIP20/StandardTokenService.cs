using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace Nethereum.StandardTokenEIP20
{
    public class StandardTokenService
    {
        private readonly Web3.Web3 web3;

        private string abi =
            @"[{""constant"":false,""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":""supply"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""from"",""type"":""address""},{""name"":""to"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""who"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""value"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""to"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":""ok"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""owner"",""type"":""address""},{""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":""_allowance"",""type"":""uint256""}],""type"":""function""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""from"",""type"":""address""},{""indexed"":true,""name"":""to"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""owner"",""type"":""address""},{""indexed"":true,""name"":""spender"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}]";

        private Contract contract;

        public StandardTokenService(Web3.Web3 web3, string address)
        {
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(abi, address);
        }

        public async Task<TNumber> GetTotalSupplyAsync<TNumber>()
        {
            var function = GetTotalSupplyFunction();
            return await function.CallAsync<TNumber>();
        }

        private Function GetTotalSupplyFunction()
        {
            return contract.GetFunction("totalSupply");
        }

        public async Task<T> GetBalanceOfAsync<T>(string address)
        {
            var function = GetBalanceOfFunction();
            return await function.CallAsync<T>(address);
        }

        private Function GetBalanceOfFunction()
        {
            return contract.GetFunction("balanceOf");
        }

        public async Task<T> GetAllowanceAsync<T>(string addressOwner, string addressSpender)
        {
            var function = GetAllowanceFunction();
            return await function.CallAsync<T>(addressOwner, addressSpender);
        }

        private Function GetAllowanceFunction()
        {
            return contract.GetFunction("allowance");
        }

        public async Task<string> TransferAsync<T>(string addressFrom, string addressTo, T value, HexBigInteger gas = null)
        {
            var function = GetTransferFunction();
           return await function.SendTransactionAsync(addressFrom, gas, null, addressTo, value);
        }

        private Function GetTransferFunction()
        {
            return contract.GetFunction("transfer");
        }

        public async Task<bool> TransferAsyncCall<T>(string addressFrom, string addressTo, T value)
        {
            var function = GetTransferFunction();
            return await function.CallAsync<bool>(addressFrom, addressTo, value);
        }

        public async Task<string> TransferFromAsync<T>(string addressFrom, string addressTransferedFrom, string addressTransferedTo,
            T value, HexBigInteger gas = null)
        {
            var function = GetTransferFromFunction();
           return await function.SendTransactionAsync(addressFrom, gas, null, addressTransferedFrom, addressTransferedTo, value);
        }

        private Function GetTransferFromFunction()
        {
            return contract.GetFunction("transferFrom");
        }

        public async Task<bool> TransferFromAsyncCall<T>(string addressFrom, string addressTransferedFrom,
            string addressTransferedTo, T value)
        {
            var function = GetTransferFromFunction();
            return await function.CallAsync<bool>(addressFrom, addressTransferedFrom, addressTransferedTo, value);
        }

        public async Task ApproveAsync<T>(string addressFrom, string addressSpender, T value, HexBigInteger gas = null)
        {
            var function = GetApproveFunction();
            await function.SendTransactionAsync(addressFrom, gas, null, addressSpender, value);
        }

        private Function GetApproveFunction()
        {
            return contract.GetFunction("approve");
        }

        public async Task<bool> ApproveAsyncCall<T>(string addressFrom, string addressSpender, T value)
        {
            var function = GetApproveFunction();
            return await function.CallAsync<bool>(addressFrom, addressSpender, value);
        }

        public Event GetApprovalEvent()
        {
            return contract.GetEvent("Approval");
        }

        public Event GetTransferEvent()
        {
            return contract.GetEvent("Transfer");
        }

    }
}
