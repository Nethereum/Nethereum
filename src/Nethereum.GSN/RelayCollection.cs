using Nethereum.GSN.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class RelayCollection : IEnumerator<Relay>, IEnumerable<Lazy<Relay>>
    {
        private readonly IRelayClient _relayClient;
        private Lazy<Relay>[] _relays;

        int position = -1;

        public Relay Current
        {
            get
            {
                try
                {
                    return _relays[position].Value;
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                };
            }
        }

        object IEnumerator.Current => Current;

        public RelayCollection(IRelayClient relayClient, IEnumerable<RelayOnChain> relays)
        {
            if (relays == null)
            {
                throw new ArgumentNullException(nameof(relays));
            }

            _relayClient = relayClient;
            _relays = relays
                .Select(x => new Lazy<Relay>(() => Get(new Relay(x))))
                .ToArray();
        }

        public void Dispose()
        {
        }

        public IEnumerator<Lazy<Relay>> GetEnumerator()
        {
            for (int index = 0; index < _relays.Length; index++)
            {
                yield return _relays[index];
            }
        }

        public bool MoveNext()
        {
            position++;
            return position < _relays.Length;
        }

        public void Reset()
        {
            position = -1;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private Relay Get(Relay relay)
        {
            return relay.IsLoaded ? relay : Load(relay).Result;
        }

        private async Task<Relay> Load(Relay relay)
        {
            try
            {
                var relayUrl = new Uri(relay.Url);
                var response = await _relayClient.GetAddrAsync(relayUrl);

                return new Relay(relay)
                {
                    Address = response.RelayServerAddress, // re-writes address from response
                    MinGasPrice = response.MinGasPrice,
                    Ready = response.Ready,
                    IsLoaded = true,
                    Version = response.Version,
                };
            }
            catch
            {
                return relay;
            }
        }
    }
}
