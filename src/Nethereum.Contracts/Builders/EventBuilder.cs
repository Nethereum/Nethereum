using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts
{
    public class EventBuilder
    {
        private readonly ContractBuilder _contract;
        private readonly EventTopicBuilder _eventTopicBuilder;

        public EventBuilder(ContractBuilder contract, EventABI eventAbi)
        {
            _contract = contract;
            EventABI = eventAbi;
            _eventTopicBuilder = new EventTopicBuilder(eventAbi);
        }

        public EventABI EventABI { get; }

        public NewFilterInput CreateFilterInput(BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = _contract.GetDefaultFilterInput(fromBlock, toBlock);
            ethFilterInput.Topics = _eventTopicBuilder.GetSignatureTopic();
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

        public bool IsLogForEvent(JToken log)
        {
            return IsLogForEvent(JsonConvert.DeserializeObject<FilterLog>(log.ToString()));
        }

        public bool IsLogForEvent(FilterLog log)
        {
            if (log.Topics != null && log.Topics.Length > 0)
            {
                var eventtopic = log.Topics[0].ToString();
                if (EventABI.Sha33Signature.IsTheSameHex(eventtopic))
                    return true;
            }
            return false;
        }


        public bool IsFilterInputForEvent(NewFilterInput filterInput)
        {
            if (filterInput.Topics != null && filterInput.Topics.Length > 0)
            {
                if (!IsFilterInputForContractAddress(filterInput))
                {
                    return false;
                }
                var eventtopic = filterInput.Topics[0].ToString();
                if (EventABI.Sha33Signature.IsTheSameHex(eventtopic))
                    return true;
            }
            return false;
        }

        private bool IsFilterInputForContractAddress(NewFilterInput filterInput)
        {
            if (filterInput.Address != null && filterInput.Address.Length > 0)
            {
                return filterInput.Address.Count(x =>
                           string.Equals(x, _contract.Address, StringComparison.CurrentCultureIgnoreCase)) > 0;
            }
            return false;
        }

        public FilterLog[] GetLogsForEvent(JArray logs)
        {
            var returnList = new List<FilterLog>();
            foreach (var log in logs)
            {
                var filterLog = JsonConvert.DeserializeObject<FilterLog>(log.ToString());
                if (IsLogForEvent(filterLog))
                    returnList.Add(filterLog);
            }
            return returnList.ToArray();
        }

        public List<EventLog<T>> DecodeAllEventsForEvent<T>(FilterLog[] logs) where T : new()
        {
            var result = new List<EventLog<T>>();
            if (logs == null) return result;
            var eventDecoder = new EventTopicDecoder();
            foreach (var log in logs)
                if (IsLogForEvent(log))
                {
                    var eventObject = eventDecoder.DecodeTopics<T>(log.Topics, log.Data);
                    result.Add(new EventLog<T>(eventObject, log));
                }
            return result;
        }

        public List<EventLog<T>> DecodeAllEventsForEvent<T>(JArray logs) where T : new()
        {
            return DecodeAllEventsForEvent<T>(GetLogsForEvent(logs));
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
    }
}