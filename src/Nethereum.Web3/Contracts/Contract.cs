using System;
using System.Linq;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3
{
    public class Contract
    {
        public Contract(RpcClient client, string abi, string contractAddress) 
        {
            this.ContractABI = new ABIDeserialiser().DeserialiseContract(abi);
            this.Client = client;
            this.Address = contractAddress;
        }


        public RpcClient Client { get; private set; }

        public ContractABI ContractABI { get; set; }

        public BlockParameter DefaultBlock { get; set; }

        public string Address { get; set; }

        public Function<TFunction> GetFunction<TFunction>()
        {
            var function = FunctionAttribute.GetAttribute<TFunction>();
            if(function == null) throw new Exception("Invalid TFunction required a Function Attribute");
            return new Function<TFunction>(Client, this, GetFunctionAbi(function.Name));
        }

        public Function GetFunction(string name)
        {  
            return new Function(Client, this, GetFunctionAbi(name));
        }

        public Event GetEvent(string name)
        {
            return new Event(Client, this, GetEventAbi(name));
        }

        private FunctionABI GetFunctionAbi(string name)
        {
            if (ContractABI == null) throw new Exception("Contract abi not initialised");
            var functionAbi = ContractABI.Functions.FirstOrDefault(x => x.Name == name);
            if (functionAbi == null) throw new Exception("Function not found");
            return functionAbi;
        }

        private EventABI GetEventAbi(string name)
        {
            if (ContractABI == null) throw new Exception("Contract abi not initialised");
            var eventAbi = ContractABI.Events.FirstOrDefault(x => x.Name == name);
            if (eventAbi == null) throw new Exception("Event not found");
            return eventAbi;
        }

    }
}