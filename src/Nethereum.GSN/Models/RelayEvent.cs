using Nethereum.GSN.DTOs;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.GSN.Models
{
    public class RelayEvent
    {
        public RelayEventType Type { get; set; }
        public BigInteger Block { get; set; }
        public string TxHash { get; set; }
        public string Address { get; set; }
        public string Url { get; set; }
        public BigInteger Fee { get; set; }
        public BigInteger Stake { get; set; }
        public BigInteger UnstakeDelay { get; set; }

        internal static RelayEvent FromEventLog(EventLog<RelayRemovedEvent> ev)
        {
            return new RelayEvent
            {
                Type = RelayEventType.Removed,
                Block = ev.Log.BlockNumber.Value,
                TxHash = ev.Log.TransactionHash,
                Address = ev.Event.Relay
            };
        }

        internal static RelayEvent FromEventLog(EventLog<RelayAddedEvent> ev)
        {
            return new RelayEvent
            {
                Type = RelayEventType.Added,
                Block = ev.Log.BlockNumber.Value,
                TxHash = ev.Log.TransactionHash,
                Address = ev.Event.Relay,
                Url = ev.Event.Url,
                Fee = ev.Event.TransactionFee,
                Stake = ev.Event.Stake,
                UnstakeDelay = ev.Event.UnstakeDelay
            };
        }
    }
}
