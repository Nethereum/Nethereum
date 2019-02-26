using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts
{
    /// <summary>
    /// Event handler for a typed EventMessage to create filters and access to logs
    /// </summary>
    /// <typeparam name="TEventMessage"></typeparam>
    public class Event<TEventMessage> : EventBase where TEventMessage : IEventDTO, new()
    {
        public Event(Contract contract) : this(contract.Eth.Client, contract.Address)
        {
        }

        public Event(IClient client, string contractAddress) : base(client, contractAddress, typeof(TEventMessage))
        {
        }

        public Event(IClient client) : this(client, null)
        {

        }

        public List<EventLog<TEventMessage>> DecodeAllEventsForEvent(JArray logs)
        {
            return EventABI.DecodeAllEvents<TEventMessage>(logs);
        }

        public List<EventLog<TEventMessage>> DecodeAllEventsForEvent(FilterLog[] logs)
        {
            return EventABI.DecodeAllEvents<TEventMessage>(logs);
        }

        public static List<EventLog<TEventMessage>> DecodeAllEvents(FilterLog[] logs)
        {
            return DecodeAllEvents<TEventMessage>(logs);
        }

        public static EventLog<TEventMessage> DecodeEvent(FilterLog log)
        {
            return GetEventABI().DecodeEvent<TEventMessage>(log);
        }

        public static EventABI GetEventABI()
        {
            return EventExtensions.GetEventABI<TEventMessage>();
        }

#if !DOTNET35
        public async Task<List<EventLog<TEventMessage>>> GetAllChanges(NewFilterInput filterInput)
        {
            if (!EventABI.IsFilterInputForEvent(ContractAddress, filterInput)) throw new Exception("Invalid filter input for current event, the event signatures, or contract addresses do not match, please use the method CreateFilterInput from this class");
            var logs = await EthGetLogs.SendRequestAsync(filterInput).ConfigureAwait(false);
            return DecodeAllEvents<TEventMessage>(logs);
        }

        public async Task<List<EventLog<TEventMessage>>> GetAllChanges(HexBigInteger filterId)
        {
            var logs = await EthFilterLogs.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<TEventMessage>(logs);
        }

        public async Task<List<EventLog<TEventMessage>>> GetFilterChanges(HexBigInteger filterId)
        {
            var logs = await EthGetFilterChanges.SendRequestAsync(filterId).ConfigureAwait(false);
            return DecodeAllEvents<TEventMessage>(logs);
        }
#endif
    }
}
