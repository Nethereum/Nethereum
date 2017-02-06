using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Services;
using Nethereum.Web3.Contracts;

namespace Nethereum.Web3
{
    public class Contract
    {
        private EthNewFilter EthNewFilter => Eth.Filters.NewFilter;

        public Contract(EthApiService eth, string abi, string contractAddress)
        {
            Eth = eth;
            ContractABI = new ABIDeserialiser().DeserialiseContract(abi);
            Address = contractAddress;
        }

        public ContractABI ContractABI { get; set; }

        public BlockParameter DefaultBlock { get; set; }

        public string Address { get; set; }

        public EthApiService Eth { get; }

        public Task<HexBigInteger> CreateFilterAsync()
        {
            var ethFilterInput = GetDefaultFilterInput();
            return EthNewFilter.SendRequestAsync(ethFilterInput);
        }

        public NewFilterInput GetDefaultFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = new NewFilterInput
            {
                FromBlock = fromBlock,
                ToBlock = toBlock ?? BlockParameter.CreateLatest(),
                Address = new[] {Address}
            };
            return ethFilterInput;
        }

        public Event GetEvent(string name)
        {
            return new Event(this, GetEventAbi(name));
        }

        public Function<TFunction> GetFunction<TFunction>()
        {
            var function = FunctionAttribute.GetAttribute<TFunction>();
            if (function == null) throw new Exception("Invalid TFunction required a Function Attribute");
            return new Function<TFunction>(this, GetFunctionAbi(function.Name));
        }

        public Function GetFunction(string name)
        {
            return new Function(this, GetFunctionAbi(name));
        }

        private EventABI GetEventAbi(string name)
        {
            if (ContractABI == null) throw new Exception("Contract abi not initialised");
            var eventAbi = ContractABI.Events.FirstOrDefault(x => x.Name == name);
            if (eventAbi == null) throw new Exception("Event not found");
            return eventAbi;
        }

        private FunctionABI GetFunctionAbi(string name)
        {
            if (ContractABI == null) throw new Exception("Contract abi not initialised");
            var functionAbi = ContractABI.Functions.FirstOrDefault(x => x.Name == name);
            if (functionAbi == null) throw new Exception("Function not found:" + name);
            return functionAbi;
        }
    }
}