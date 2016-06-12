using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3
{
    public class Event
    {
        private IClient client;
        private EventABI eventABI;

        private Contract contract;
        private EthNewFilter ethNewFilter;
        private EthGetFilterChangesForEthNewFilter ethGetFilterChanges;
        private EventTopicBuilder eventTopicBuilder;
        private EthGetFilterLogsForEthNewFilter ethFilterLogs;


        public Task<HexBigInteger> CreateFilterAsync(BlockParameter fromBlock = null)
        {
            var ethFilterInput = contract.GetDefaultFilterInput(fromBlock);
            ethFilterInput.Topics = new[] { eventTopicBuilder.GetSignaguteTopic()};
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T>(T firstIndexedParameterValue, BlockParameter fromBlock = null)
        {
            return CreateFilterAsync(new object[] {firstIndexedParameterValue}, fromBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1 firstIndexedParameterValue, T2 secondIndexedParameterValue, BlockParameter fromBlock = null)
        {
            return CreateFilterAsync(new object[] { firstIndexedParameterValue }, new object[] { secondIndexedParameterValue }, fromBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1 firstIndexedParameterValue, T2 secondIndexedParameterValue, T3 thirdIndexedParameterValue,  BlockParameter fromBlock = null)
        {
            return CreateFilterAsync(new object[] {firstIndexedParameterValue},
                new object[] {secondIndexedParameterValue}, new object[] {thirdIndexedParameterValue}, fromBlock);
        }


        public Task<HexBigInteger> CreateFilterAsync<T>(T[] firstIndexedParameterValues, BlockParameter fromBlock = null)
        {
            return CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(), fromBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1[] firstIndexedParameterValues, T2[] secondIndexedParameterValues, BlockParameter fromBlock = null)
        {
            return CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(), secondIndexedParameterValues.Cast<object>().ToArray(), fromBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1[] firstIndexedParameterValues, T2[] secondIndexedParameterValues, T3[] thirdIndexedParameterValues, BlockParameter fromBlock = null)
        {
            return  CreateFilterAsync( firstIndexedParameterValues.Cast<object>().ToArray(), secondIndexedParameterValues.Cast<object>().ToArray(), thirdIndexedParameterValues.Cast<object>().ToArray(), fromBlock);
        }


        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, BlockParameter fromBlock = null)
        {
            var ethFilterInput = contract.GetDefaultFilterInput(fromBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1);
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync( object[] filterTopic1, object[] filterTopic2, BlockParameter fromBlock = null)
        {
            var ethFilterInput = contract.GetDefaultFilterInput(fromBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2);
             return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3, BlockParameter fromBlock = null)
        {
            var ethFilterInput = contract.GetDefaultFilterInput(fromBlock);

            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }


        //public async Task<HexBigInteger> CreateFilterAsync<T>(T firstIndexedParameterValue)
        //{
        //    return await CreateFilterAsync<T>(null, firstIndexedParameterValue);
        //}

        //public async Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1 firstIndexedParameterValue, T2 secondIndexedParameterValue)
        //{
        //    return await CreateFilterAsync(new object[] { firstIndexedParameterValue }, new object[] { secondIndexedParameterValue });
        //}

        //public async Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1 firstIndexedParameterValue, T2 secondIndexedParameterValue, T3 thirdIndexedParameterValue)
        //{
        //    return await CreateFilterAsync(new object[] { firstIndexedParameterValue }, new object[] { secondIndexedParameterValue }, new object[] { thirdIndexedParameterValue });
        //}


        //public async Task<HexBigInteger> CreateFilterAsync<T>(T[] firstIndexedParameterValues)
        //{
        //    return await CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray());
        //}

        //public async Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1[] firstIndexedParameterValues, T2[] secondIndexedParameterValues)
        //{
        //    return await CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(), secondIndexedParameterValues.Cast<object>().ToArray());
        //}

        //public async Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1[] firstIndexedParameterValues, T2[] secondIndexedParameterValues, T3[] thirdIndexedParameterValues)
        //{
        //    return await CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(), secondIndexedParameterValues.Cast<object>().ToArray(), thirdIndexedParameterValues.Cast<object>().ToArray());
        //}


        //public async Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1)
        //{
        //    var ethFilterInput = contract.GetDefaultFilterInput();
        //    ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1);
        //    return await ethNewFilter.SendRequestAsync(ethFilterInput);
        //}

        //public async Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2)
        //{
        //    var ethFilterInput = contract.GetDefaultFilterInput();
        //    ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2);
        //    return await ethNewFilter.SendRequestAsync(ethFilterInput);
        //}

        //public async Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3)
        //{
        //    var ethFilterInput = contract.GetDefaultFilterInput();
            
        //    ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2, filterTopic3);
        //    return await ethNewFilter.SendRequestAsync(ethFilterInput);
        //}

       

    

        public Event(IClient client, Contract contract, EventABI eventABI)
        {
            this.client = client;
            this.contract = contract;
            this.eventABI = eventABI;
            this.eventTopicBuilder = new EventTopicBuilder(eventABI);
            ethNewFilter = new EthNewFilter(client);
            ethGetFilterChanges = new EthGetFilterChangesForEthNewFilter(client);
            ethFilterLogs = new EthGetFilterLogsForEthNewFilter(client);

        }

        public async Task<List<EventLog<T>>> GetAllChanges<T>(HexBigInteger filterId) where T : new()
        {
            var logs = await ethFilterLogs.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<T>(logs);
        }

        public async Task<List<EventLog<T>>> GetFilterChanges<T>(HexBigInteger filterId) where T : new()
        {
            var logs = await ethGetFilterChanges.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<T>(logs);
        }

        public List<EventLog<T>> DecodeAllEvents<T>(FilterLog[] logs) where T : new()
        {
            var result = new List<EventLog<T>>();
            var eventDecoder = new EventTopicDecoder();
            foreach (var log in logs)
            {
                var eventObject = eventDecoder.DecodeTopics<T>(log.Topics, log.Data);
                result.Add(new EventLog<T>(eventObject, log));
            }
            return result;
        }
    }

    public class EventLog<T>
    {
        public EventLog(T eventObject, FilterLog log)
        {
            Event = eventObject;
            Log = log;
        }

        public T Event { get; private set; }
        public FilterLog Log { get; private set; }
    }
}