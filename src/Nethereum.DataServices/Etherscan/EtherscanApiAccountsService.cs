using Nethereum.DataServices.Etherscan.Responses;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Etherscan
{
    public class EtherscanApiAccountsService
    {

        public EtherscanRequestService EtherscanRequestService { get; private set; }

        public EtherscanApiAccountsService(EtherscanRequestService etherscanRequestService)
        {
            EtherscanRequestService = etherscanRequestService;
        }

        public Task<EtherscanResponse<List<EtherscanGetAccountTransactionsResponse>>> GetAccountTransactionsAsync(string address, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            return GetAccountTransactionsAsync(address, 0, BigInteger.Parse("999999999999999"), page, offset, sort);
        }


        public async Task<EtherscanResponse<List<EtherscanGetAccountTransactionsResponse>>> GetAccountTransactionsAsync(string address, BigInteger startBlock, BigInteger endBlock, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            var url = $"{EtherscanRequestService.BaseUrl}api?module=account&action=txlist&address={address}&startblock={startBlock}&endblock{endBlock}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanGetAccountTransactionsResponse>>(url).ConfigureAwait(false);
        }

        public Task<EtherscanResponse<List<EtherscanGetAccountInternalTransactionsResponse>>> GetAccountInternalTransactionsAsync(string address, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            return GetAccountInternalTransactionsAsync(address, 0, BigInteger.Parse("999999999999999"), page, offset, sort);
        }

        public async Task<EtherscanResponse<List<EtherscanGetAccountInternalTransactionsResponse>>> GetAccountInternalTransactionsAsync(string address, BigInteger startBlock, BigInteger endBlock, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            var url = $"{EtherscanRequestService.BaseUrl}api?module=account&action=txlistinternal&address={address}&startblock={startBlock}&endblock{endBlock}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanGetAccountInternalTransactionsResponse>>(url).ConfigureAwait(false);
        }
    }
}
