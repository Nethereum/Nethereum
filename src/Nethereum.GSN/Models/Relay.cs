using System.Numerics;

namespace Nethereum.GSN.Models
{
    public class Relay : RelayOnChain
    {
        public BigInteger MinGasPrice { get; set; }

        public bool Ready { get; set; } = false;

        public bool IsLoaded { get; set; } = false;

        public string Version { get; set; }

        public Relay() { }

        public Relay(RelayOnChain relay)
        {
            Address = relay.Address;
            Url = relay.Url;
            Fee = relay.Fee;
            Stake = relay.Stake;
            UnstakeDelay = relay.UnstakeDelay;
        }
    }
}
