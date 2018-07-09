using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Contracts
{
    public class Contract
    {
        public Contract(EthApiService eth, string abi, string contractAddress)
        {
            Eth = eth;
            ContractBuilder = new ContractBuilder(abi, contractAddress);
        }

        public Contract(EthApiService eth, Type contractMessageType, string contractAddress)
        {
            Eth = eth;
            ContractBuilder = new ContractBuilder(contractMessageType, contractAddress);
        }

        public Contract(EthApiService eth, Type[] contractMessagesTypes, string contractAddress)
        {
            Eth = eth;
            ContractBuilder = new ContractBuilder(contractMessagesTypes, contractAddress);
        }

        private EthNewFilter EthNewFilter => Eth.Filters.NewFilter;

        public ContractBuilder ContractBuilder { get; set; }

        public string Address => ContractBuilder.Address;

        public EthApiService Eth { get; }

        public Task<HexBigInteger> CreateFilterAsync()
        {
            var ethFilterInput = GetDefaultFilterInput();
            return EthNewFilter.SendRequestAsync(ethFilterInput);
        }

        public NewFilterInput GetDefaultFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return ContractBuilder.GetDefaultFilterInput(fromBlock, toBlock);
        }

        public Event GetEvent(string name)
        {
            return new Event(this, ContractBuilder.GetEventAbi(name));
        }

        public Event<T> GetEvent<T>(string name) where T: new()
        {
            return new Event<T>(this);
        }

        public Function<TFunction> GetFunction<TFunction>()
        {
            return new Function<TFunction>(this, GetFunctionBuilder<TFunction>());
        }

        public Function GetFunction(string name)
        {
            return new Function(this, GetFunctionBuilder(name));
        }

        private EventBuilder GetEventBuilder(string name)
        {
            return ContractBuilder.GetEventBuilder(name);
        }

        private FunctionBuilder GetFunctionBuilder(string name)
        {
            return ContractBuilder.GetFunctionBuilder(name);
        }

        private FunctionBuilder<TFunctionInput> GetFunctionBuilder<TFunctionInput>()
        {
            return ContractBuilder.GetFunctionBuilder<TFunctionInput>();
        }
    }
}