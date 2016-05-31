using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Web.Sample.Model.Dao
{
    [FunctionOutput]
    public class Proposal
    {
        public long Index { get; set; }

        [Parameter("address", 1)]
        public string Recipient { get; set; }

        [Parameter("uint256", 2)]
        public BigInteger Amount { get; set; }

        [Parameter("string", 3)]
        public string Description { get; set; }

        [Parameter("uint256", 4)]
        public BigInteger VotingDeadline { get; set; }

        [Parameter("bool", 5)]
        public bool Open { get; set; }

        [Parameter("bool", 6)]
        public bool ProposalPassed { get; set; }

        [Parameter("bytes32", 7)]
        public byte[] ProposalHash { get; set; }

        public string GetProposalHashToHex()
        {
            return ProposalHash.ToHex();
        }

        [Parameter("uint256", 8)]
        public BigInteger ProposalDeposit { get; set; }

        [Parameter("bool", 9)]
        public bool NewCurator { get; set; }

        [Parameter("uint256", 10)]
        public BigInteger Yea { get; set; }

        [Parameter("uint256", 11)]
        public BigInteger Nay { get; set; }

        [Parameter("address", 12)]
        public string Creator { get; set; }
    }
}