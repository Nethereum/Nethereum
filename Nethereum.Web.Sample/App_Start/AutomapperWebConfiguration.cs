using AutoMapper;
using Nethereum.Web.Sample.Model.Dao;
using Nethereum.Web.Sample.ViewModels;

namespace Nethereum.Web.Sample
{
    public static class AutomapperWebConfiguration
    {
        public static void Configure()
        {
            ConfigureProposalsMapping();
        }

        private static void ConfigureProposalsMapping()
        {
            Mapper.Initialize(cfg => cfg.CreateMap<Proposal, ProposalViewModel>());

        }
    }
}