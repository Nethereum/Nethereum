using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts
{
    public class Event
    {
        private readonly Contract _contract;

        public Event(Contract contract, EventBuilder @event)
        {
            _contract = contract;
            EventBuilder = @event;
        }

        private EthGetFilterLogsForEthNewFilter EthFilterLogs => _contract.Eth.Filters.GetFilterLogsForEthNewFilter;

        private EthGetFilterChangesForEthNewFilter EthGetFilterChanges
            => _contract.Eth.Filters.GetFilterChangesForEthNewFilter;

        private EthGetLogs EthGetLogs => _contract.Eth.Filters.GetLogs;
        private EthNewFilter EthNewFilter => _contract.Eth.Filters.NewFilter;

        protected EventBuilder EventBuilder { get; }

        public Task<HexBigInteger> CreateFilterAsync(BlockParameter fromBlock = null)
        {
            var newFilterInput = CreateFilterInput(fromBlock);
            return EthNewFilter.SendRequestAsync(newFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync<T>(T firstIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return CreateFilterAsync(new object[] {firstIndexedParameterValue}, fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return CreateFilterAsync(new object[] {firstIndexedParameterValue},
                new object[] {secondIndexedParameterValue}, fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1 firstIndexedParameterValue,
            T2 secondIndexedParameterValue, T3 thirdIndexedParameterValue, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return CreateFilterAsync(new object[] {firstIndexedParameterValue},
                new object[] {secondIndexedParameterValue}, new object[] {thirdIndexedParameterValue}, fromBlock,
                toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T>(T[] firstIndexedParameterValues,
            BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(),
                secondIndexedParameterValues.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync<T1, T2, T3>(T1[] firstIndexedParameterValues,
            T2[] secondIndexedParameterValues, T3[] thirdIndexedParameterValues, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return CreateFilterAsync(firstIndexedParameterValues.Cast<object>().ToArray(),
                secondIndexedParameterValues.Cast<object>().ToArray(),
                thirdIndexedParameterValues.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, fromBlock, toBlock);
            return EthNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, filterTopic2, fromBlock, toBlock);
            return EthNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2,
            object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = CreateFilterInput(filterTopic1, filterTopic2, filterTopic3, fromBlock, toBlock);
            return EthNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterBlockRangeAsync(BlockParameter fromBlock, BlockParameter toBlock)
        {
            var newFilterInput = CreateFilterInput(fromBlock, toBlock);
            return EthNewFilter.SendRequestAsync(newFilterInput);
        }

        public NewFilterInput CreateFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventBuilder.CreateFilterInput(fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            return EventBuilder.CreateFilterInput(filterTopic1, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventBuilder.CreateFilterInput(filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return EventBuilder.CreateFilterInput(filterTopic1, filterTopic2, filterTopic3, fromBlock, toBlock);
        }

        public static List<EventLog<T>> DecodeAllEvents<T>(FilterLog[] logs) where T : new()
        {
            return EventBuilder.DecodeAllEvents<T>(logs);
        }

#if !DOTNET35
        public async Task<List<EventLog<T>>> GetAllChanges<T>(NewFilterInput filterInput) where T : new()
        {
            if(!EventBuilder.IsFilterInputForEvent(filterInput)) throw new Exception("Invalid filter input for current event, use CreateFilterInput");
            var logs = await EthGetLogs.SendRequestAsync(filterInput).ConfigureAwait(false);
            return DecodeAllEvents<T>(logs);
        }

        public async Task<List<EventLog<T>>> GetAllChanges<T>(HexBigInteger filterId) where T : new()
        {
            var logs = await EthFilterLogs.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<T>(logs);
        }

        public async Task<List<EventLog<T>>> GetFilterChanges<T>(HexBigInteger filterId) where T : new()
        {
            var logs = await EthGetFilterChanges.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<T>(logs);
        }
#else

#endif
        public bool IsLogForEvent(JToken log)
        {
            return EventBuilder.IsLogForEvent(log);
        }

        public bool IsLogForEvent(FilterLog log)
        {
            return EventBuilder.IsLogForEvent(log);
        }

        public FilterLog[] GetLogsForEvent(JArray logs)
        {
            return EventBuilder.GetLogsForEvent(logs);
        }

        public List<EventLog<T>> DecodeAllEventsForEvent<T>(FilterLog[] logs) where T : new()
        {
            return EventBuilder.DecodeAllEventsForEvent<T>(logs);
        }

        public List<EventLog<T>> DecodeAllEventsForEvent<T>(JArray logs) where T : new()
        {
            return EventBuilder.DecodeAllEventsForEvent<T>(logs);
        }
    }
}