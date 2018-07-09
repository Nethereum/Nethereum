using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts.Extensions
{

    public static class EventExtensions
    {
        public static bool IsLogForEvent<TEventDTO>(this JToken log)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.IsLogForEvent(log);
        }

        public static bool IsLogForEvent<TEventDTO>(this FilterLog log)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.IsLogForEvent(log);
        }

        public static bool IsFilterInputForEvent<TEventDTO>(string contractAddress,
            NewFilterInput filterInput)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.IsFilterInputForEvent(contractAddress, filterInput);
        }

        public static bool IsLogForEvent(this EventABI eventABI, JToken log)
        {
            return IsLogForEvent(eventABI, JsonConvert.DeserializeObject<FilterLog>(log.ToString()));
        }

        public static bool IsLogForEvent(this EventABI eventABI, FilterLog log)
        {
            return IsLogForEvent(log, eventABI.Sha33Signature);
        }

        public static bool IsLogForEvent(this FilterLog log, string signature)
        {
            if (log.Topics != null && log.Topics.Length > 0)
            {
                var eventtopic = log.Topics[0].ToString();
                if (signature.IsTheSameHex(eventtopic))
                    return true;
            }
            return false;
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, filterTopic3, fromBlock,
                toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string[] contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string[] contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string[] contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(this TEventDTO eventDTO, string[] contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null) where TEventDTO : IEventDTO
        {
            var eventABI = GetEventABI<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, filterTopic3, fromBlock,
                toBlock);
        }

        public static EventABI GetEventABI<TEventDTO>() where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI;
        }

        public static EventABI GetEventABI<TEventDTO>(this TEventDTO eventDTO) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI;
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, filterTopic3, fromBlock,
                toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string[] contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string[] contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string[] contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<TEventDTO>(string[] contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.CreateFilterInput(contractAddress, filterTopic1, filterTopic2, filterTopic3, fromBlock,
                toBlock);
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetSignatureTopic();
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetSignatureTopic();
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventTopicBuilder = new EventTopicBuilder(eventABI);
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventTopicBuilder.GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static bool IsFilterInputForEvent(this EventABI eventABI, string contractAddress, NewFilterInput filterInput)
        {
            if (filterInput.Topics != null && filterInput.Topics.Length > 0)
            {
                if (!IsFilterInputForContractAddress(contractAddress, filterInput))
                {
                    return false;
                }
                var eventtopic = filterInput.Topics[0].ToString();
                if (eventABI.Sha33Signature.IsTheSameHex(eventtopic))
                    return true;
            }
            return false;
        }

        public static bool IsFilterInputForContractAddress(string contractAdress, NewFilterInput filterInput)
        {
            if (filterInput.Address != null && filterInput.Address.Length > 0)
            {
                return filterInput.Address.Count(x =>
                           string.Equals(x, contractAdress, StringComparison.CurrentCultureIgnoreCase)) > 0;
            }
            return false;
        }

        public static FilterLog[] GetLogsForEvent(this EventABI eventABI, JArray logs)
        {
            var returnList = new List<FilterLog>();
            foreach (var log in logs)
            {
                var filterLog = JsonConvert.DeserializeObject<FilterLog>(log.ToString());
                if (IsLogForEvent(eventABI, filterLog))
                    returnList.Add(filterLog);
            }
            return returnList.ToArray();
        }

        public static List<EventLog<TEventDTO>> DecodeAllEvents<TEventDTO>(this JArray logs) where TEventDTO : new()
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.DecodeAllEvents<TEventDTO>(logs);
        }


        public static List<EventLog<TEventDTO>> DecodeAllEvents<TEventDTO>(this FilterLog[] logs) where TEventDTO : new()
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return DecodeAllEvents<TEventDTO>(eventABI, logs);
        }

        public static List<EventLog<TEventDTO>> DecodeAllEvents<TEventDTO>(this EventABI eventABI, JArray logs) where TEventDTO : new()
        {
            return DecodeAllEvents<TEventDTO>(eventABI, GetLogsForEvent(eventABI, logs));
        }

        public static List<EventLog<TEventDTO>> DecodeAllEvents<TEventDTO>(this EventABI eventABI, FilterLog[] logs) where TEventDTO : new()
        {
            var result = new List<EventLog<TEventDTO>>();
            if (logs == null) return result;
         
            foreach (var log in logs)
            {
                var eventDecoded = DecodeEvent<TEventDTO>(eventABI, log);
                if (eventDecoded != null)
                {
                    result.Add(eventDecoded);
                }
            }
            return result;
        }

        public static EventLog<TEventDTO> DecodeEvent<TEventDTO>(this EventABI eventABI, FilterLog log) where TEventDTO : new()
        {
            if (!IsLogForEvent(eventABI, log)) return null;
            var eventDecoder = new EventTopicDecoder();
            var eventObject = eventDecoder.DecodeTopics<TEventDTO>(log.Topics, log.Data);
            return new EventLog<TEventDTO>(eventObject, log);
        }

        public static EventLog<TEventDTO> DecodeEvent<TEventDTO>(this FilterLog log) where TEventDTO : new()
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return eventABI.DecodeEvent<TEventDTO>(log);
        }

        public static TEventDTO DecodeEvent<TEventDTO>(this TEventDTO eventDTO, JToken log) where TEventDTO : IEventDTO
        {
            var filterLog = JsonConvert.DeserializeObject<FilterLog>(log.ToString());
            return DecodeEvent<TEventDTO>(eventDTO, filterLog);
        }

        public static TEventDTO DecodeEvent<TEventDTO>(this TEventDTO eventDTO, FilterLog log) where TEventDTO : IEventDTO
        {
            var eventABI = ABITypedRegistry.GetEvent<TEventDTO>();
            return DecodeEvent<TEventDTO>(eventDTO, eventABI, log);
        }

        public static TEventDTO DecodeEvent<TEventDTO>(this TEventDTO eventDTO, EventABI eventABI, FilterLog log) where TEventDTO : IEventDTO
        {
            if (!IsLogForEvent(eventABI, log)) return default(TEventDTO);
            var eventDecoder = new EventTopicDecoder();
            return eventDecoder.DecodeTopics(eventDTO, log.Topics, log.Data);
        }

        public static bool IsLogForEvent<TEventDTO>(this TEventDTO eventDTO, JToken log) where TEventDTO : IEventDTO
        {
            return IsLogForEvent<TEventDTO>(log);
        }

        public static bool IsLogForEvent<TEventDTO>(this TEventDTO eventDTO, FilterLog log) where TEventDTO : IEventDTO
        {
            return IsLogForEvent<TEventDTO>(log);
        }
    }
}