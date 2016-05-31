using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Nethereum.Web.Sample.Model.Dao;
using Nethereum.Web.Sample.Services;
using Nethereum.Web.Sample.ViewModels;
using System.Numerics;

namespace Nethereum.Web.Sample.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        private DaoService GetDaoService()
        {
            var web3 = new Web3.Web3();
            var service = new DaoService(web3, "0xbb9bc244d798123fde783fcc1c72d3bb8c189413");
            return service;
        }
      
        public async Task<ActionResult> DaoProposals()
        {
            var service = GetDaoService();          
            var proposals = await service.GetAllProposals();
            var proposalsViewModel = AutoMapper.Mapper.Map<List<ProposalViewModel>>(proposals);
            return View(proposalsViewModel);
        }

        public async Task<ActionResult> DaoProposalDetail(long index)
        {
            var service = GetDaoService();
            var proposal = await service.GetProposal(index);
            var proposalViewModel = AutoMapper.Mapper.Map<ProposalViewModel>(proposal);
            return View(proposalViewModel);
        }

    }
}