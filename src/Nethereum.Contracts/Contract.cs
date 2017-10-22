using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using System.Reflection;
using System.Collections.Generic;

namespace Nethereum.Contracts
{
    public class Contract
    {
        private BlockParameter defaultBlock;

        public Contract(EthApiService eth, string abi, string contractAddress)
        {
            Eth = eth;
            ContractBuilder = new ContractBuilder(abi, contractAddress);
            DefaultBlock = eth.DefaultBlock;
        }

        public Contract(EthApiService eth, Type contractMessageType, string contractAddress)
        {
            Eth = eth;
            var abiExtractor = new AttributesToABIExtractor();
            ContractBuilder = new ContractBuilder(contractMessageType, contractAddress);
            DefaultBlock = eth.DefaultBlock;
        }

        public Contract(EthApiService eth, Type[] contractMessagesTypes, string contractAddress)
        {
            Eth = eth;
            var abiExtractor = new AttributesToABIExtractor();
            ContractBuilder = new ContractBuilder(contractMessagesTypes, contractAddress);
            DefaultBlock = eth.DefaultBlock;
        }

        private EthNewFilter EthNewFilter => Eth.Filters.NewFilter;

        public ContractBuilder ContractBuilder { get; set; }

        public BlockParameter DefaultBlock
        {
            get { return defaultBlock; }
            set
            {
                defaultBlock = value;
                SetDefaultBlock();
            }
        }

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
            return new Event(this, GetEventBuilder(name));
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

        private void SetDefaultBlock()
        {
            if (ContractBuilder != null)
            {
                ContractBuilder.DefaultBlock = DefaultBlock;
            }
        }

    }
}