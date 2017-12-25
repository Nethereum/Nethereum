using Nethereum.Web.Sample.Services;
using Nethereum.Web.Sample.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web.Sample.Controllers
{
    public class TestNetController : Controller
    {
        private readonly TestNetService _testNetService; 

        public TestNetController()
        {
            _testNetService = new TestNetService();
        }

        // GET: TestNet/Id
        public async Task<ActionResult> Index(int id = 1)
        {
            var address = _testNetService.EndpointAddress(id);
            
            var web3 = new Web3.Web3(address);

            var lastBockInChaing = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());

            var transactions = lastBockInChaing.Transactions.Select(x => new TransactionViewModel
            {
                TransactionHash = x.TransactionHash,
                From = x.From,
                To = x.To,
                Value = Web3.Web3.Convert.FromWei(x.Value).ToString()
            }).ToList();


            var testNets = new SelectedTestNetViewModel
            {
                SelectedTestnetId = id,
                TestNetList = _testNetService.AllTestNets(),
                EndpointUrl = address,
                Transactions = transactions
            };

            return View(testNets);
        }
    }
}
