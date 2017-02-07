using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Contracts
{
    public class EventLog<T> : IEventLog
    {
        public EventLog(T eventObject, FilterLog log)
        {
            Event = eventObject;
            Log = log;
        }

        public T Event { get; private set; }
        public FilterLog Log { get; }
    }
}