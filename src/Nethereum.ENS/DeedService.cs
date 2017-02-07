using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace Nethereum.ENS
{
    public class DeedService
    {
        public static string ABI =
            @"[{'constant':true,'inputs':[],'name':'creationDate','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'destroyDeed','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'newOwner','type':'address'}],'name':'setOwner','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'registrar','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'owner','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'refundRatio','type':'uint256'}],'name':'closeDeed','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'newRegistrar','type':'address'}],'name':'setRegistrar','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'newValue','type':'uint256'}],'name':'setBalance','outputs':[],'payable':true,'type':'function'},{'inputs':[],'type':'constructor'},{'payable':true,'type':'fallback'},{'anonymous':false,'inputs':[{'indexed':false,'name':'newOwner','type':'address'}],'name':'OwnerChanged','type':'event'},{'anonymous':false,'inputs':[],'name':'DeedClosed','type':'event'}]";

        public static string BYTE_CODE =
            "0x6060604052600080546c0100000000000000000000000033810204600160a060020a0319909116179055426001556002805460a060020a60ff02191674010000000000000000000000000000000000000000179055610372806100626000396000f36060604052361561006c5760e060020a600035046305b34410811461006e5780630b5ab3d51461007c57806313af4035146100895780632b20e397146100af5780638da5cb5b146100c6578063bbe42771146100dd578063faab9d3914610103578063fb1669ca14610129575b005b346100025761014a60015481565b346100025761006c610189565b346100025761006c60043560005433600160a060020a039081169116146101f557610002565b34610002576101a0600054600160a060020a031681565b34610002576101a0600254600160a060020a031681565b346100025761006c60043560005433600160a060020a0390811691161461026657610002565b346100025761006c60043560005433600160a060020a039081169116146102d657610002565b61006c60043560005433600160a060020a0390811691161461030b57610002565b60408051918252519081900360200190f35b6040517fbb2ce2f51803bba16bc85282b47deeea9a5c6223eabea1077be696b3f265cf1390600090a16102635b60025460a060020a900460ff16156101bc57610002565b60408051600160a060020a039092168252519081900360200190f35b600254604051600160a060020a039182169130163180156108fc02916000818181858888f19350505050156101f05761deadff5b610002565b6002805473ffffffffffffffffffffffffffffffffffffffff19166c010000000000000000000000008381020417905560408051600160a060020a038316815290517fa2ea9883a321a3e97b8266c2b078bfeec6d50c711ed71f874a90d500ae2eaf36916020908290030190a15b50565b60025460a060020a900460ff16151561027e57610002565b6002805474ff00000000000000000000000000000000000000001916905560405161dead906103e8600160a060020a03301631848203020480156108fc02916000818181858888f19350505050151561015c57610002565b600080546c010000000000000000000000008084020473ffffffffffffffffffffffffffffffffffffffff1990911617905550565b60025460a060020a900460ff16151561032357610002565b8030600160a060020a031631101561033a57610002565b600254604051600160a060020a039182169130163183900380156108fc02916000818181858888f1935050505015156102635761000256";

        private readonly Web3.Web3 web3;

        private readonly Contract contract;

        public DeedService(Web3.Web3 web3, string address)
        {
            this.web3 = web3;
            contract = web3.Eth.GetContract(ABI, address);
        }

        public Task<string> CloseDeedAsync(string addressFrom, BigInteger refundRatio, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionCloseDeed();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, refundRatio);
        }

        public Task<BigInteger> CreationDateAsyncCall()
        {
            var function = GetFunctionCreationDate();
            return function.CallAsync<BigInteger>();
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, string addressFrom, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            return web3.Eth.DeployContract.SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount);
        }

        public Task<string> DestroyDeedAsync(string addressFrom, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionDestroyDeed();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount);
        }

        public Event GetEventDeedClosed()
        {
            return contract.GetEvent("DeedClosed");
        }

        public Event GetEventOwnerChanged()
        {
            return contract.GetEvent("OwnerChanged");
        }

        public Function GetFunctionCloseDeed()
        {
            return contract.GetFunction("closeDeed");
        }

        public Function GetFunctionCreationDate()
        {
            return contract.GetFunction("creationDate");
        }

        public Function GetFunctionDestroyDeed()
        {
            return contract.GetFunction("destroyDeed");
        }

        public Function GetFunctionOwner()
        {
            return contract.GetFunction("owner");
        }

        public Function GetFunctionRegistrar()
        {
            return contract.GetFunction("registrar");
        }

        public Function GetFunctionSetBalance()
        {
            return contract.GetFunction("setBalance");
        }

        public Function GetFunctionSetOwner()
        {
            return contract.GetFunction("setOwner");
        }

        public Function GetFunctionSetRegistrar()
        {
            return contract.GetFunction("setRegistrar");
        }

        public Task<string> OwnerAsyncCall()
        {
            var function = GetFunctionOwner();
            return function.CallAsync<string>();
        }

        public Task<string> RegistrarAsyncCall()
        {
            var function = GetFunctionRegistrar();
            return function.CallAsync<string>();
        }

        public Task<string> SetBalanceAsync(string addressFrom, BigInteger newValue, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionSetBalance();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, newValue);
        }

        public Task<string> SetOwnerAsync(string addressFrom, string newOwner, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionSetOwner();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, newOwner);
        }

        public Task<string> SetRegistrarAsync(string addressFrom, string newRegistrar, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionSetRegistrar();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, newRegistrar);
        }
    }
}