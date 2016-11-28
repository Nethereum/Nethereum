using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3
{
    public class Contract
    {
        private readonly EthNewFilter ethNewFilter;

        public Contract(IClient client, string abi, string contractAddress)
        {
            ContractABI = new ABIDeserialiser().DeserialiseContract(abi);
            Client = client;
            Address = contractAddress;
            ethNewFilter = new EthNewFilter(client);
        }

        public IClient Client { get; }

        public ContractABI ContractABI { get; set; }

        public BlockParameter DefaultBlock { get; set; }

        public string Address { get; set; }

        public Task<HexBigInteger> CreateFilterAsync()
        {
            var ethFilterInput = GetDefaultFilterInput();
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public NewFilterInput GetDefaultFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = new NewFilterInput();
            ethFilterInput.FromBlock = fromBlock;
            ethFilterInput.ToBlock = toBlock ?? BlockParameter.CreateLatest();
            ethFilterInput.Address = new[] {Address};
            return ethFilterInput;
        }

        public Event GetEvent(string name)
        {
            return new Event(Client, this, GetEventAbi(name));
        }

        public Function<TFunction> GetFunction<TFunction>()
        {
            var function = FunctionAttribute.GetAttribute<TFunction>();
            if (function == null) throw new Exception("Invalid TFunction required a Function Attribute");
            return new Function<TFunction>(Client, this, GetFunctionAbi(function.Name));
        }

        public Function GetFunction(string name)
        {
            return new Function(Client, this, GetFunctionAbi(name));
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