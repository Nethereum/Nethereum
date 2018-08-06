using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
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
            return IsLogForEvent(log, eventABI.Sha3Signature);
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

        public static EventTopicBuilder GetTopicBuilder(this EventABI eventABI)
        {
            return new EventTopicBuilder(eventABI);
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetSignatureTopicAsTheOnlyTopic();
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetSignatureTopicAsTheOnlyTopic();
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, BlockParameter fromBlock = null,
            BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, string contractAddress, T1 filterTopic1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics( filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, string[] contractAddress, T1 filterTopic1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, T1 filterTopic1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput((string)null, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1);
            return ethFilterInput;
        }
        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, object[] filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, string contractAddress, T1 filterTopic1, T2 filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, string[] contractAddress, T1 filterTopic1, T2 filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics( filterTopic1,  filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, T1 filterTopic1, T2 filterTopic2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput((string)null, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput(this EventABI eventABI, string[] contractAddress, object[] filterTopic1, object[] filterTopic2, object[] filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1, T2, T3>(this EventABI eventABI, string contractAddress, T1 filterTopic1, T2 filterTopic2, T3 filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1,T2,T3>(this EventABI eventABI, string[] contractAddress, T1 filterTopic1, T2 filterTopic2, T3 filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput(contractAddress, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1,T2,T3>(this EventABI eventABI, T1 filterTopic1, T2 filterTopic2, T3 filterTopic3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var ethFilterInput = FilterInputBuilder.GetDefaultFilterInput((string)null, fromBlock, toBlock);
            ethFilterInput.Topics = eventABI.GetTopicBuilder().GetTopics(filterTopic1, filterTopic2, filterTopic3);
            return ethFilterInput;
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, T1[] filterOrTopics1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(filterOrTopics1.Cast<object>().ToArray()
                , fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, string contractAddress, T1[] filterOrTopics1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddress, filterOrTopics1.Cast<object>().ToArray(), 
                 fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1>(this EventABI eventABI, string[] contractAddresses, T1[] filterOrTopics1,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddresses, filterOrTopics1.Cast<object>().ToArray(),
                fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, T1[] filterOrTopics1, T2[] filterOrTopics2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray()
                , fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, string contractAddress, T1[] filterOrTopics1, T2[] filterOrTopics2,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddress, filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray()
                , fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2>(this EventABI eventABI, string[] contractAddresses, T1[] filterOrTopics1, T2[] filterOrTopics2, 
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddresses, filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray(),
                fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2, T3>(this EventABI eventABI, T1[] filterOrTopics1, T2[] filterOrTopics2, T3[] filterOrTopics3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray(),
                filterOrTopics3.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2, T3>(this EventABI eventABI, string contractAddress, T1[] filterOrTopics1, T2[] filterOrTopics2, T3[] filterOrTopics3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddress, filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray(),
                filterOrTopics3.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public static NewFilterInput CreateFilterInput<T1, T2, T3>(this EventABI eventABI, string[] contractAddresses, T1[] filterOrTopics1, T2[] filterOrTopics2, T3[] filterOrTopics3,
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return eventABI.CreateFilterInput(contractAddresses, filterOrTopics1.Cast<object>().ToArray(), filterOrTopics2.Cast<object>().ToArray(),
                filterOrTopics3.Cast<object>().ToArray(), fromBlock, toBlock);
        }

        public static bool IsFilterInputForEvent(this EventABI eventABI, string contractAddress, NewFilterInput filterInput)
        {
            if (filterInput.Topics != null && filterInput.Topics.Length > 0)
            {
                if (!IsFilterInputForContractAddress(contractAddress, filterInput))
                {
                    return false;
                }
                if(eventABI.IsTopicSignatureForEvent(filterInput.Topics[0]))
                    return true;
            }
            return false;
        }

        public static bool IsFilterInputForContractAddress(string contractAdress, NewFilterInput filterInput)
        {
            if (contractAdress == null) return true;
            if (filterInput.Address != null && filterInput.Address.Length > 0)
            {
                return filterInput.Address.Count(x =>
                           string.Equals(x, contractAdress, StringComparison.CurrentCultureIgnoreCase)) > 0;
            }
            return false;
        }

        public static bool IsTopicSignatureForEvent(this EventABI eventABI, object topic)
        {
            if (topic is IEnumerable<object> topicArray)
            {
                return topicArray.Any(x => eventABI.Sha3Signature.IsTheSameHex(x.ToString()));
            }
            else
            {
                if (topic is string topicString)
                {
                    return eventABI.Sha3Signature.IsTheSameHex(topicString);
                }
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

        public static List<EventLog<TEventDTO>> DecodeAllEvents<TEventDTO>(this TransactionReceipt transactionReceipt) where TEventDTO : new()
        {
            return transactionReceipt.Logs.DecodeAllEvents<TEventDTO>();
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