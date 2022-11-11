using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts
{

    public class EventBase
    {
        protected EthGetFilterChangesForEthNewFilter EthGetFilterChanges { get; }
        protected EthGetLogs EthGetLogs { get; }
        protected EthNewFilter EthNewFilter { get; }
        public string ContractAddress { get; }
        public EventABI EventABI { get; }
        protected EthGetFilterLogsForEthNewFilter EthFilterLogs { get; set; }

        public EventBase(IClient client, string contractAddress, EventABI eventABI)
        {
            EthFilterLogs = new EthGetFilterLogsForEthNewFilter(client);
            EthGetFilterChanges = new EthGetFilterChangesForEthNewFilter(client);
            EthGetLogs = new EthGetLogs(client);
            EthNewFilter = new EthNewFilter(client);
            ContractAddress = contractAddress;
            EventABI = eventABI;
        }

        public EventBase(IClient client, string contractAddress, Type eventABIType)
        {
            EthFilterLogs = new EthGetFilterLogsForEthNewFilter(client);
            EthGetFilterChanges = new EthGetFilterChangesForEthNewFilter(client);
            EthGetLogs = new EthGetLogs(client);
            EthNewFilter = new EthNewFilter(client);
            ContractAddress = contractAddress;
            EventABI = ABITypedRegistry.GetEvent(eventABIType);
        }

        public Task<HexBigInteger> CreateFilterAsync(BlockParameter fromBlock = null)
        {
            var newFilterInput = CreateFilterInput(fromBlock, (BlockParameter)null);
            return EthNewFilter.SendRequestAsync(newFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T>(T firstIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValue, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValue, secondIndexedParameterValue, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, T3 thirdIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValue, secondIndexedParameterValue, thirdIndexedParameterValue, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T>(T[] firstIndexedParameterValues,
            BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValues, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValues, secondIndexedParameterValues, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, T3[] thirdIndexedParameterValues, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var filterInput = CreateFilterInput(firstIndexedParameterValues, secondIndexedParameterValues, thirdIndexedParameterValues, fromBlock, toBlock);
            return CreateFilterAsync(filterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, fromBlock, toBlock);
            return CreateFilterAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, filterTopic2, fromBlock, toBlock);
            return CreateFilterAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2,
            object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, filterTopic2, filterTopic3, fromBlock, toBlock);
            return CreateFilterAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(NewFilterInput newfilterInput)
        {
            return EthNewFilter.SendRequestAsync(newfilterInput);
        }

        public NewFilterInput CreateFilterInput<T>(T firstIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValue,
                fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput<T1, T2>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValue, secondIndexedParameterValue,
                 fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput<T1, T2, T3>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, T3 thirdIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValue, secondIndexedParameterValue,
                thirdIndexedParameterValue, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput<T>(T[] firstIndexedParameterValues,
            BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValues,
                 fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput<T1, T2>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValues, secondIndexedParameterValues,
                 fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput<T1, T2, T3>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, T3[] thirdIndexedParameterValues, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, firstIndexedParameterValues, secondIndexedParameterValues,
                thirdIndexedParameterValues, fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterBlockRangeAsync(BlockParameter fromBlock, BlockParameter toBlock)
        { 
            var newFilterInput = CreateFilterInput(fromBlock, toBlock);
            return CreateFilterAsync(newFilterInput);
        }

        public NewFilterInput CreateFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, filterTopic1, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventABI.CreateFilterInput(ContractAddress, filterTopic1, filterTopic2, filterTopic3, fromBlock, toBlock);
        }

        public bool IsLogForEvent(JToken log)
        {
            return EventABI.IsLogForEvent(log);
        }

        public bool IsLogForEvent(FilterLog log)
        {
            return EventABI.IsLogForEvent(log);
        }

        public FilterLog[] GetLogsForEvent(JArray logs)
        {
            return EventABI.GetLogsForEvent(logs);
        }

        public static List<EventLog<T>> DecodeAllEvents<T>(FilterLog[] logs) where T : new()
        {
            return logs.DecodeAllEvents<T>();
        }
    }
}