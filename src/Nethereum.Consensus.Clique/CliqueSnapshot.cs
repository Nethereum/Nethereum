using Nethereum.Util;

namespace Nethereum.Consensus.Clique
{
    public class CliqueSnapshot
    {
        public long BlockNumber { get; set; }
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public List<string> Signers { get; set; } = new();
        public Dictionary<string, CliqueVote> Votes { get; set; } = new();
        public Dictionary<string, int> VoteTally { get; set; } = new();
        public Dictionary<long, string> Recents { get; set; } = new();

        public bool IsAuthorized(string address)
        {
            return Signers.Any(s => s.IsTheSameAddress(address));
        }

        public bool ValidVote(string target, bool authorize)
        {
            return IsAuthorized(target) != authorize;
        }

        public int SignerIndex(string address)
        {
            return Signers.FindIndex(s => s.IsTheSameAddress(address));
        }

        public int TotalSigners => Signers.Count;

        public int RequiredVotes => (TotalSigners / 2) + 1;

        public bool SignedWithinLimit(string signer, long blockNumber, int limit)
        {
            foreach (var entry in Recents)
            {
                if (entry.Value.IsTheSameAddress(signer) && entry.Key > blockNumber - limit)
                    return true;
            }
            return false;
        }

        public void RecordSigner(long blockNumber, string signer, int limit)
        {
            Recents[blockNumber] = signer;
            if (blockNumber >= limit)
                Recents.Remove(blockNumber - limit);
        }

        public static List<string> NormalizedAndSorted(IEnumerable<string> signers)
        {
            return signers
                .Select(AddressUtil.Current.ConvertToValid20ByteAddressLowerCase)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }

        public CliqueSnapshot Clone()
        {
            return new CliqueSnapshot
            {
                BlockNumber = BlockNumber,
                BlockHash = (byte[])BlockHash.Clone(),
                Signers = new List<string>(Signers),
                Votes = new Dictionary<string, CliqueVote>(Votes),
                VoteTally = new Dictionary<string, int>(VoteTally),
                Recents = new Dictionary<long, string>(Recents)
            };
        }
    }

    public class CliqueVote
    {
        public string Signer { get; set; } = "";
        public string Target { get; set; } = "";
        public bool Authorize { get; set; }
        public long BlockNumber { get; set; }
    }

}
