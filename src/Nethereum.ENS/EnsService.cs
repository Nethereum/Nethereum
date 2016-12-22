using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;

namespace Nethereum.ENS
{
   public partial class EnsService
   {
        private readonly Web3.Web3 web3;

        public static string ABI = @"[{'constant':true,'inputs':[{'name':'node','type':'bytes32'}],'name':'resolver','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'node','type':'bytes32'}],'name':'owner','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'label','type':'bytes32'},{'name':'owner','type':'address'}],'name':'setSubnodeOwner','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'ttl','type':'uint64'}],'name':'setTTL','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'node','type':'bytes32'}],'name':'ttl','outputs':[{'name':'','type':'uint64'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'resolver','type':'address'}],'name':'setResolver','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'node','type':'bytes32'},{'name':'owner','type':'address'}],'name':'setOwner','outputs':[],'payable':false,'type':'function'},{'inputs':[],'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'node','type':'bytes32'},{'indexed':true,'name':'label','type':'bytes32'},{'indexed':false,'name':'owner','type':'address'}],'name':'NewOwner','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'node','type':'bytes32'},{'indexed':false,'name':'owner','type':'address'}],'name':'Transfer','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'node','type':'bytes32'},{'indexed':false,'name':'resolver','type':'address'}],'name':'NewResolver','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'node','type':'bytes32'},{'indexed':false,'name':'ttl','type':'uint64'}],'name':'NewTTL','type':'event'}]";

        public static string BYTE_CODE = "0x606060405260008080526020527fad3228b676f7d3cd4284a5443f17f1962b36e491b30a40b2405849e597ba5fb580546c0100000000000000000000000033810204600160a060020a031990911617905561044b8061005e6000396000f3606060405236156100615760e060020a60003504630178b8bf811461006657806302571be31461009257806306ab5923146100ba57806314ab9038146100f657806316a25cbd1461012f5780631896f70a146101635780635b0fc9c31461019c575b610002565b34610002576101d5600435600081815260208190526040902060010154600160a060020a03165b919050565b34610002576101d5600435600081815260208190526040902054600160a060020a031661008d565b34610002576101f16004356024356044356000838152602081905260408120548490600160a060020a0390811633919091161461021057610002565b34610002576101f16004356024356000828152602081905260409020548290600160a060020a039081163391909116146102b357610002565b34610002576101f360043560008181526020819052604090206001015467ffffffffffffffff60a060020a9091041661008d565b34610002576101f16004356024356000828152602081905260409020548290600160a060020a0390811633919091161461035657610002565b34610002576101f16004356024356000828152602081905260409020548290600160a060020a039081163391909116146103d257610002565b60408051600160a060020a039092168252519081900360200190f35b005b6040805167ffffffffffffffff9092168252519081900360200190f35b60408051868152602080820187905282519182900383018220600160a060020a03871683529251929450869288927fce0457fe73731f824cc272376169235128c118b49d344817417c6d108d155e8292908290030190a382600060005060008460001916815260200190815260200160002060005060000160006101000a815481600160a060020a030219169083606060020a9081020402179055505050505050565b6040805167ffffffffffffffff84168152905184917f1d4f9bbfc9cab89d66e1a1562f2233ccbf1308cb4f63de2ead5787adddb8fa68919081900360200190a26000838152602081905260409020600101805478010000000000000000000000000000000000000000000000008085020460a060020a027fffffffff0000000000000000ffffffffffffffffffffffffffffffffffffffff909116179055505050565b60408051600160a060020a0384168152905184917f335721b01866dc23fbee8b6b2c7b1e14d6f05c28cd35a2c934239f94095602a0919081900360200190a260008381526020819052604090206001018054606060020a8085020473ffffffffffffffffffffffffffffffffffffffff19909116179055505050565b60408051600160a060020a0384168152905184917fd4735d920b0f87494915f556dd9b54c8f309026070caea5c737245152564d266919081900360200190a260008381526020819052604090208054606060020a8085020473ffffffffffffffffffffffffffffffffffffffff1990911617905550505056";

        public static Task<string> DeployContractAsync(Web3.Web3 web3, string addressFrom,  HexBigInteger gas = null, HexBigInteger valueAmount = null) 
        {
            return web3.Eth.DeployContract.SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount );
        }

        private Contract contract;

        public EnsService(Web3.Web3 web3, string address)
        {
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(ABI, address);
        }

        public Function GetFunctionResolver() {
            return contract.GetFunction("resolver");
        }
        public Function GetFunctionOwner() {
            return contract.GetFunction("owner");
        }
        public Function GetFunctionSetSubnodeOwner() {
            return contract.GetFunction("setSubnodeOwner");
        }
        public Function GetFunctionSetTTL() {
            return contract.GetFunction("setTTL");
        }
        public Function GetFunctionTtl() {
            return contract.GetFunction("ttl");
        }
        public Function GetFunctionSetResolver() {
            return contract.GetFunction("setResolver");
        }
        public Function GetFunctionSetOwner() {
            return contract.GetFunction("setOwner");
        }

        public Event GetEventNewOwner() {
            return contract.GetEvent("NewOwner");
        }
        public Event GetEventTransfer() {
            return contract.GetEvent("Transfer");
        }
        public Event GetEventNewResolver() {
            return contract.GetEvent("NewResolver");
        }
        public Event GetEventNewTTL() {
            return contract.GetEvent("NewTTL");
        }

        public Task<string> ResolverAsyncCall(byte[] node) {
            var function = GetFunctionResolver();
            return function.CallAsync<string>(node);
        }
        public Task<string> OwnerAsyncCall(byte[] node) {
            var function = GetFunctionOwner();
            return function.CallAsync<string>(node);
        }
        public Task<ulong> TtlAsyncCall(byte[] node) {
            var function = GetFunctionTtl();
            return function.CallAsync<ulong>(node);
        }

        public Task<string> SetSubnodeOwnerAsync(string addressFrom, byte[] node, byte[] label, string owner, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetSubnodeOwner();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, label, owner);
        }
        public Task<string> SetTTLAsync(string addressFrom, byte[] node, ulong ttl, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetTTL();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, ttl);
        }
        public Task<string> SetResolverAsync(string addressFrom, byte[] node, string resolver, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetResolver();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, resolver);
        }
        public Task<string> SetOwnerAsync(string addressFrom, byte[] node, string owner, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionSetOwner();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, node, owner);
        }
    }


    public class NewOwnerEventDTO 
    {
        [Parameter("bytes32", "node", 1, true)]
        public byte[] Node {get; set;}

        [Parameter("bytes32", "label", 2, true)]
        public byte[] Label {get; set;}

        [Parameter("address", "owner", 3, false)]
        public string Owner {get; set;}

    }

    public class TransferEventDTO 
    {
        [Parameter("bytes32", "node", 1, true)]
        public byte[] Node {get; set;}

        [Parameter("address", "owner", 2, false)]
        public string Owner {get; set;}

    }

    public class NewResolverEventDTO 
    {
        [Parameter("bytes32", "node", 1, true)]
        public byte[] Node {get; set;}

        [Parameter("address", "resolver", 2, false)]
        public string Resolver {get; set;}

    }

    public class NewTTLEventDTO 
    {
        [Parameter("bytes32", "node", 1, true)]
        public byte[] Node {get; set;}

        [Parameter("uint64", "ttl", 2, false)]
        public ulong Ttl {get; set;}
    }
}

