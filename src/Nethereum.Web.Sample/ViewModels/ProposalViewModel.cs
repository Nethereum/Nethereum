using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Web.UI.WebControls;
using Nethereum.Web.Sample.Model.Dao;

namespace Nethereum.Web.Sample.ViewModels
{
    public class ProposalViewModelMapper
    {
        private const int EthDecimals = 18;
        private const int DaoDecimals = 16;

        private decimal ConvertToUnit(BigInteger amount, int numberDecimalPlaces)
        {
            return (decimal) amount/(decimal)(Math.Pow(10,numberDecimalPlaces));
        }

        public List<ProposalViewModel> MapFromModel(IEnumerable<Proposal> proposals)
        {
            return proposals.Select(MapFromModel).ToList();
        }

        public ProposalViewModel MapFromModel(Proposal proposal)
        {
            var vm = new ProposalViewModel
            {
                Index = proposal.Index,
                Recipient = proposal.Recipient,
                Amount = ConvertToUnit(proposal.Amount, EthDecimals),
                Creator = proposal.Creator,
                Description = proposal.Description,
                Nay = ConvertToUnit(proposal.Nay, DaoDecimals),
                NewCurator = proposal.NewCurator,
                Open = proposal.Open,
                ProposalDeposit = ConvertToUnit(proposal.ProposalDeposit, EthDecimals),
                ProposalPassed = proposal.ProposalPassed,
                Yea = ConvertToUnit(proposal.Yea, DaoDecimals),
                VotingDeadline = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((double)proposal.VotingDeadline)
            };
            return vm;
        }
    }

    public class ProposalViewModel
    {
        public long Index { get; set; }
        public string Recipient { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime VotingDeadline { get; set; }
        public bool Open { get; set; }
        public bool ProposalPassed { get; set; }
        
        public decimal ProposalDeposit { get; set; }
        public bool NewCurator { get; set; }
        public decimal Yea { get; set; }
        public decimal Nay { get; set; }
        public string Creator { get; set; }
    }
}