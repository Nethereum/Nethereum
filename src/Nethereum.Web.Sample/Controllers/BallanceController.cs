using System.Globalization;
using System.Threading.Tasks;
using System.Web.Http;
using Nethereum.Web.Sample.Model;

namespace Nethereum.Web.Sample.Controllers
{
    public class BallanceController : ApiController
    {
        public async Task<string> Post(GetBallanceParam ballanseFor)
        {
            var web3 = new Web3.Web3(ballanseFor.EndpointUrl);

            return Web3.Web3.Convert.FromWei(await web3.Eth.GetBalance.SendRequestAsync(ballanseFor.AccountAddress)).ToString(CultureInfo.InvariantCulture);
        }
    }
}