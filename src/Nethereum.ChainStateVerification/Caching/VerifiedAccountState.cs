using System;
using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.ChainStateVerification.Caching
{
    public class VerifiedAccountState
    {
        public string Address { get; }
        public Account Account { get; set; }
        public byte[] Code { get; set; }
        public bool CodeVerified { get; set; }
        public Dictionary<string, byte[]> Storage { get; } = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public VerifiedAccountState(string address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }
    }
}
