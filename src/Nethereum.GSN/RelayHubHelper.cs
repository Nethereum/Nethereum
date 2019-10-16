using Nethereum.GSN.DTOs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class RelayHubHelper
    {
        public static Task<TransactionReceipt> DepositRelayHub(
            string privateKey,
            string endpoint,
            string relayHubAddress,
            string target,
            BigInteger value)
        {
            var account = new Account(privateKey);
            var web3 = new Web3.Web3(account, endpoint);

            var message = new DepositForFunction()
            {
                Target = target,
                AmountToSend = value
            };
            var handler = web3.Eth.GetContractTransactionHandler<DepositForFunction>();
            return handler.SendRequestAndWaitForReceiptAsync(relayHubAddress, message);
        }
    }
}
