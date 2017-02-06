using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;

namespace Nethereum.ENS
{
   public partial class PublicResolverService
   {
        private readonly Web3.Web3 web3;

        public static string ABI = @"[{'constant':true,'inputs':[{'name':'interfaceID','type':'bytes4'}],'name':'supportsInterface','outputs':[{'name':'','type':'bool'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'node','type':'bytes32'}],'name':'content','outputs':[{'name':'ret','type':'bytes32'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'node','type':'bytes32'}],'name':'addr','outputs':[{'name':'ret','type':'address'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'node','type':'bytes32'},{'name':'kind','type':'bytes32'}],'name':'has','outputs':[{'name':'','type':'bool'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'hash','type':'bytes32'}],'name':'setContent','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'addr','type':'address'}],'name':'setAddr','outputs':[],'payable':false,'type':'function'},{'inputs':[{'name':'ensAddr','type':'address'}],'type':'constructor'},{'payable':false,'type':'fallback'}]";

        public static string BYTE_CODE = "0x60606040526040516020806103aa833950608060405251600080546c0100000000000000000000000080840204600160a060020a0319909116179055506103608061004a6000396000f3606060405236156100565760e060020a600035046301ffc9a781146100635780632dff6941146100d75780633b3b57de1461010057806341b9dc2b1461013c578063c3d014d6146101da578063d5fa2b0014610267575b34610002576102f4610002565b34610002576102f660043560007f3b3b57de00000000000000000000000000000000000000000000000000000000600160e060020a0319831614806100d157507fd8389dc500000000000000000000000000000000000000000000000000000000600160e060020a03198316145b92915050565b346100025760043560009081526002602052604090205460408051918252519081900360200190f35b3461000257600435600090815260016020526040902054600160a060020a031660408051600160a060020a039092168252519081900360200190f35b34610002576102f660043560243560007f6164647200000000000000000000000000000000000000000000000000000000821480156101915750600083815260016020526040902054600160a060020a031615155b806101d357507f6861736800000000000000000000000000000000000000000000000000000000821480156101d3575060008381526002602052604090205415155b9392505050565b34610002576102f460043560243560008054604080516020908101849052815160e060020a6302571be30281526004810187905291518694600160a060020a033381169516936302571be393602480830194919391928390030190829087803b156100025760325a03f11561000257505060405151600160a060020a031691909114905061030a57610002565b34610002576102f460043560243560008054604080516020908101849052815160e060020a6302571be30281526004810187905291518694600160a060020a033381169516936302571be393602480830194919391928390030190829087803b156100025760325a03f11561000257505060405151600160a060020a031691909114905061031d57610002565b005b604080519115158252519081900360200190f35b5060009182526002602052604090912055565b600083815260016020526040902080546c010000000000000000000000008085020473ffffffffffffffffffffffffffffffffffffffff1990911617905550505056";

        public static Task<string> DeployContractAsync(Web3.Web3 web3, string addressFrom, string ensAddr, HexBigInteger gas = null, HexBigInteger valueAmount = null) 
        {
            return web3.Eth.GetDeployContract().SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount , ensAddr);
        }

        private Contract contract;

        public PublicResolverService(Web3.Web3 web3, string address)
        {
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(ABI, address);
        }

        public Function GetFunctionSupportsInterface() {
            return contract.GetFunction("supportsInterface");
        }
        public Function GetFunctionContent() {
            return contract.GetFunction("content");
        }
        public Function GetFunctionAddr() {
            return contract.GetFunction("addr");
        }
        public Function GetFunctionHas() {
            return contract.GetFunction("has");
        }
        public Function GetFunctionSetContent() {
            return contract.GetFunction("setContent");
        }
        public Function GetFunctionSetAddr() {
            return contract.GetFunction("setAddr");
        }

        public Task<bool> SupportsInterfaceAsyncCall(byte[] interfaceID) {
            var function = GetFunctionSupportsInterface();
            return function.CallAsync<bool>(interfaceID);
        }
        public Task<byte[]> ContentAsyncCall(byte[] node) {
            var function = GetFunctionContent();
            return function.CallAsync<byte[]>(node);
        }
        public Task<string> AddrAsyncCall(byte[] node) {
            var function = GetFunctionAddr();
            return function.CallAsync<string>(node);
        }
        public Task<bool> HasAsyncCall(byte[] node, byte[] kind) {
            var function = GetFunctionHas();
            return function.CallAsync<bool>(node, kind);
        }

        public Task<string> SetContentAsync(string addressFrom, byte[] node, byte[] hash, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetContent();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, hash);
        }
        public Task<string> SetAddrAsync(string addressFrom, byte[] node, string addr, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetAddr();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, addr);
        }
    }
}

