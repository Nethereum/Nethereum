using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Web3;

namespace Uport
{
   public class UportRegistryService
   {
        private readonly Web3 web3;

        public static string ABI = @"[{'constant':true,'inputs':[{'name':'registrationIdentifier','type':'bytes32'},{'name':'issuer','type':'address'},{'name':'subject','type':'address'}],'name':'get','outputs':[{'name':'','type':'string[]'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'version','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'previousPublishedVersion','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'bytes32'},{'name':'','type':'address'},{'name':'','type':'address'}],'name':'registry','outputs':[{'name':'','type':'bytes32'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'registrationIdentifier','type':'bytes32'},{'name':'subject','type':'address'},{'name':'value','type':'bytes32'}],'name':'set','outputs':[],'payable':false,'type':'function'},{'inputs':[{'name':'_previousPublishedVersion','type':'address'}],'payable':false,'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'registrationIdentifier','type':'bytes32'},{'indexed':true,'name':'issuer','type':'address'},{'indexed':true,'name':'subject','type':'address'},{'indexed':false,'name':'updatedAt','type':'uint256'}],'name':'Set','type':'event'}]";

        public static string BYTE_CODE = "6060604052341561000c57fe5b60405160208061029f83398101604052515b600360005560018054600160a060020a031916600160a060020a0383161790555b505b61024f806100506000396000f300606060405263ffffffff60e060020a600035041663447885f0811461004d57806354fd4d50146100845780636104464f146100a657806381895b73146100d2578063d79d8e6c14610109575bfe5b341561005557fe5b610072600435600160a060020a036024358116906044351661012d565b60408051918252519081900360200190f35b341561008c57fe5b610072610164565b60408051918252519081900360200190f35b34156100ae57fe5b6100b661016a565b60408051600160a060020a039092168252519081900360200190f35b34156100da57fe5b610072600435600160a060020a0360243581169060443516610179565b60408051918252519081900360200190f35b341561011157fe5b61012b600435600160a060020a036024351660443561019c565b005b6000838152600260209081526040808320600160a060020a03808716855290835281842090851684529091529020545b9392505050565b60005481565b600154600160a060020a031681565b600260209081526000938452604080852082529284528284209052825290205481565b81600160a060020a031633600160a060020a031684600019167feaf626c2c2ec7b7bd4328ffad20cd8bf2e631858020a5a4a0b4ea02276af3e91426040518082815260200191505060405180910390a46000838152600260209081526040808320600160a060020a033381168552908352818420908616845290915290208190555b5050505600a165627a7a72305820fc8e8d07110bd485a217e6dc55d1c8ad8e3e74e73705ee61efab9188aadf43920029";

		public static Task<string> DeployContractAsync(Web3 web3, string addressFrom, string _previousPublishedVersion, HexBigInteger gas = null, HexBigInteger valueAmount = null) 
        {
            return web3.Eth.DeployContract.SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount , _previousPublishedVersion );
        }

        private Contract contract;

        public UportRegistryService(Web3 web3, string address)
        {
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(ABI, address);
        }

        public Function GetFunctionGet()
        {
            return contract.GetFunction("get");
        }

        public Function GetFunctionVersion()
        {
            return contract.GetFunction("version");
        }

        public Function GetFunctionPreviousPublishedVersion()
        {
            return contract.GetFunction("previousPublishedVersion");
        }

        public Function GetFunctionRegistry()
        {
            return contract.GetFunction("registry");
        }

        public Function GetFunctionSet()
        {
            return contract.GetFunction("set");
        }

        public Event GetEventSet()
        {
            return contract.GetEvent("Set");
        }

        public Task<List<string>> GetAsyncCall(byte[] registrationIdentifier, string issuer, string subject)
        {
           var function = GetFunctionGet();
           return function.CallAsync<List<string>>(registrationIdentifier, issuer, subject);
        }

        public Task<BigInteger> VersionAsyncCall()
        {
           var function = GetFunctionVersion();
           return function.CallAsync<BigInteger>();
        }

        public Task<string> PreviousPublishedVersionAsyncCall()
        {
           var function = GetFunctionPreviousPublishedVersion();
           return function.CallAsync<string>();
        }

        public Task<byte[]> RegistryAsyncCall(byte[] b, string c, string d)
        {
           var function = GetFunctionRegistry();
           return function.CallAsync<byte[]>(b, c, d);
        }

        public Task<string> SetAsync(string addressFrom, byte[] registrationIdentifier, string subject, byte[] value, HexBigInteger gas = null, HexBigInteger valueAmount = null)
        {
           var function = GetFunctionSet();
           return function.SendTransactionAsync(addressFrom, gas, valueAmount, registrationIdentifier, subject, value);
        }
    }


    

    public class SetEventDTO
    {

        [Parameter("bytes32", "registrationIdentifier", 1, true)]
        public byte[] RegistrationIdentifier { get; set; }

        [Parameter("address", "issuer", 2, true)]
        public string Issuer { get; set; }

        [Parameter("address", "subject", 3, true)]
        public string Subject { get; set; }

        [Parameter("uint256", "updatedAt", 4, false)]
        public BigInteger UpdatedAt { get; set; }
    }

}
