using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.CoreChain.Models
{
    public class LogFilter
    {
        public BigInteger? FromBlock { get; set; }
        public BigInteger? ToBlock { get; set; }
        public List<string> Addresses { get; set; }
        public List<List<byte[]>> Topics { get; set; }

        public LogFilter()
        {
            Addresses = new List<string>();
            Topics = new List<List<byte[]>>();
        }

        public bool MatchesAddress(string address)
        {
            if (Addresses == null || Addresses.Count == 0)
                return true;

            var normalizedAddress = address?.ToLowerInvariant();
            foreach (var filterAddress in Addresses)
            {
                if (filterAddress?.ToLowerInvariant() == normalizedAddress)
                    return true;
            }
            return false;
        }

        public bool MatchesTopics(List<byte[]> logTopics)
        {
            if (Topics == null || Topics.Count == 0)
                return true;

            for (int i = 0; i < Topics.Count; i++)
            {
                var topicFilter = Topics[i];
                if (topicFilter == null || topicFilter.Count == 0)
                    continue;

                if (i >= logTopics.Count)
                    return false;

                var logTopic = logTopics[i];
                bool matchesAny = false;
                foreach (var filterTopic in topicFilter)
                {
                    if (ByteArraysEqual(filterTopic, logTopic))
                    {
                        matchesAny = true;
                        break;
                    }
                }
                if (!matchesAny)
                    return false;
            }
            return true;
        }

        public bool MatchesBlockRange(BigInteger blockNumber)
        {
            if (FromBlock.HasValue && blockNumber < FromBlock.Value)
                return false;
            if (ToBlock.HasValue && blockNumber > ToBlock.Value)
                return false;
            return true;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
