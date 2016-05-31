using System.Numerics;

namespace Nethereum.Web.Sample.ViewModels
{
    public class ProposalViewModel
    {
        public long Index { get; set; }
        public string Recipient { get; set; }
        public string Amount { get; set; }
        public string Description { get; set; }
        public string VotingDeadline { get; set; }
        public bool Open { get; set; }
        public bool ProposalPassed { get; set; }
        
        public string ProposalDeposit { get; set; }
        public bool NewCurator { get; set; }
        public string Yea { get; set; }
        public string Nay { get; set; }
        public string Creator { get; set; }
    }
}