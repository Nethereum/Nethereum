using System;
using System.Linq;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Generic;

namespace Nethereum.Web3
{
    public class Contract
    {
        public Contract(RpcClient client, string abi, string contractAddress) : this(client, contractAddress)
        {
            this.ContractABI = new ABIDeserialiser().DeserialiseContract(abi);
        }

        public Contract(RpcClient client, string contractAddress)
        {
            this.Client = client;
            this.Address = contractAddress;
        }

        public RpcClient Client { get; private set; }

        public ContractABI ContractABI { get; set; }

        public BlockParameter DefaultBlock { get; set; }

        public string DefaultAccount { get; set; }

        public string Address { get; set; }

        public Function<TFunction> GetFunction<TFunction>()
        {
            return new Function<TFunction>(Client, this);
        }

        public Function GetFunction(string name)
        {
            if (ContractABI == null) throw new Exception("Contract abi not initialised");
            var functionAbi = ContractABI.Functions.FirstOrDefault(x => x.Name == name);
            if (functionAbi == null) throw new Exception("Function not found");
            return new Function(Client, this, functionAbi);
        }

    }
}