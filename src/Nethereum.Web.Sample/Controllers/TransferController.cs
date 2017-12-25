using System.Threading.Tasks;
using System.Web.Http;
using Nethereum.Hex.HexTypes;
using Nethereum.Web.Sample.Model;
using Nethereum.Web3.Accounts;

namespace Nethereum.Web.Sample.Controllers
{
    public class TransferController : ApiController
    {
        // POST: api/Transfer
        public async  Task<string> Post(TransferParam transferProperties)
        {
            var account = new Account(transferProperties.PrivateKey);

            var web3 = new Web3.Web3(account, transferProperties.EndpointUrl);

            var transactionReceipt = await web3.TransactionManager.TransactionReceiptService.SendRequestAsync(
                () => web3.TransactionManager.SendTransactionAsync(account.Address, transferProperties.To,
                    new HexBigInteger(Web3.Web3.Convert.ToWei(transferProperties.Amount)))
                );

            return transactionReceipt.Status.Value == 0 ? "Fail" : "Success";
        }
    }
}
