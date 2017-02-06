using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Web3
{
    public class Event
    {
        private readonly Contract _contract;
        private EthGetFilterLogsForEthNewFilter EthFilterLogs => _contract.Eth.Filters.GetFilterLogsForEthNewFilter;
        private EthGetFilterChangesForEthNewFilter EthGetFilterChanges => _contract.Eth.Filters.GetFilterChangesForEthNewFilter;
        private EthGetLogs EthGetLogs => _contract.Eth.Filters.GetLogs;
        private EthNewFilter EthNewFilter => _contract.Eth.Filters.NewFilter;

        private readonly EventTopicBuilder _eventTopicBuilder;

        public Event(Contract contract, EventABI eventAbi)
        {
            _contract = contract;
            EventABI = eventAbi;
            _eventTopicBuilder = new EventTopicBuilder(eventAbi);
        }

        public EventABI EventABI { get; }

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


        public Task<HexBigInteger> CreateFilterAsync<T>(T[] firstIndexedParameterValues, BlockParameter fromBlock = null,
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

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
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
            var ethFilterInput = _contract.GetDefaultFilterInput(fromBlock, toBlock);
            ethFilterInput.Topics = new[] {_eventTopicBuilder.GetSignaguteTopic()};
            return ethFilterInput;
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var ethFilterInput = _contract.GetDefaultFilterInput(fromBlock, toBlock);
            ethFilterInput.Topics = _eventTopicBuilder.GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = _contract.GetDefaultFilterInput(fromBlock, toBlock);
            ethFilterInput.Topics = _eventTopicBuilder.GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public NewFilterInput CreateFilterInput(object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = _contract.GetDefaultFilterInput(fromBlock, toBlock);
            ethFilterInput.Topics = _eventTopicBuilder.GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static List<EventLog<T>> DecodeAllEvents<T>(FilterLog[] logs) where T : new()
        {
            var result = new List<EventLog<T>>();
            if (logs == null) return result;
            var eventDecoder = new EventTopicDecoder();
            foreach (var log in logs)
            {
                var eventObject = eventDecoder.DecodeTopics<T>(log.Topics, log.Data);
                result.Add(new EventLog<T>(eventObject, log));
            }
            return result;
        }

        public async Task<List<EventLog<T>>> GetAllChanges<T>(NewFilterInput filterInput) where T : new()
        {
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

        public bool IsLogForEvent(JToken log)
        {
            return IsLogForEvent(JsonConvert.DeserializeObject<FilterLog>(log.ToString()));
        }

        public bool IsLogForEvent(FilterLog log)
        {
            if ((log.Topics != null) && (log.Topics.Length > 0))
            {
                var eventtopic = log.Topics[0].ToString();
                if (EventABI.Sha33Signature.IsTheSameHex(eventtopic))
                    return true;
            }
            return false;
        }
    }
}