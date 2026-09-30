using System.Collections.Generic;

namespace Nethereum.Contracts.Standards.ENS
{
    public class EnsRecord
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public Dictionary<string, string> Texts { get; set; } = new Dictionary<string, string>();
        public byte[] ContentHash { get; set; }
    }
}
