using System;
using Nethereum.Documentation;

namespace Nethereum.DevP2P.Peering
{
    [NethereumDocExample(DocSection.DevP2P, "devp2p", "DialCandidate — outbound dial reservation key")]
    public sealed class DialCandidate
    {
        public string Key { get; }

        public bool IsTrusted { get; }

        public DialCandidate(string key, bool isTrusted = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            Key = key;
            IsTrusted = isTrusted;
        }
    }
}
