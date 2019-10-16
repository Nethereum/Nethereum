using Nethereum.GSN.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.GSN
{
    public class RelayCollection : IEnumerable<Relay>, IEnumerator<Relay>
    {
        private readonly IRelayClient _relayClient;

        private RelayOnChain[] _relaysOnChain;
        private Relay[] _relays;

        int position = -1;

        public Relay Current
        {
            get
            {
                try
                {
                    return _relays[position];
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
            _relayClient = relayClient;

            _relaysOnChain = new RelayOnChain[relays.Count()];
            _relays = new Relay[relays.Count()];

            for (int i = 0; i < relays.Count(); i++)
            {
                _relaysOnChain[i] = relays.ElementAt(i);
            }
        }

        public void Dispose()
        {
        }

        public IEnumerator<Relay> GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            position++;
            if (_relays[position] == null && position < _relaysOnChain.Length)
            {
                try
                {
                    var relayUrl = new Uri(_relaysOnChain[position].Url);
                    var response = _relayClient.GetAddrAsync(relayUrl).Result;
                    _relays[position] = new Relay(_relaysOnChain[position])
                    {
                        Address = response.RelayServerAddress, // re-writes address from response
                        MinGasPrice = response.MinGasPrice,
                        Ready = response.Ready,
                        Version = response.Version,
                    };
                }
                catch
                {
                    _relays[position] = new Relay(_relaysOnChain[position]);
                }
            }
            return position < _relaysOnChain.Length;
        }

        public void Reset()
        {
            position = -1;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
