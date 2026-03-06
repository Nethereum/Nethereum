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

        public bool IsAuthorized(string address)
        {
            return Signers.Any(s => s.IsTheSameAddress(address));
        }

        public int SignerIndex(string address)
        {
            return Signers.FindIndex(s => s.IsTheSameAddress(address));
        }

        public int TotalSigners => Signers.Count;

        public int RequiredVotes => (TotalSigners / 2) + 1;

        public CliqueSnapshot Clone()
        {
            return new CliqueSnapshot
            {
                BlockNumber = BlockNumber,
                BlockHash = (byte[])BlockHash.Clone(),
                Signers = new List<string>(Signers),
                Votes = new Dictionary<string, CliqueVote>(Votes),
                VoteTally = new Dictionary<string, int>(VoteTally)
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

    public class CliqueSigner
    {
        public string Address { get; set; } = "";
        public bool IsAuthorized { get; set; }
        public long LastSignedBlock { get; set; }
    }
}
