using Nethereum.DataServices.Etherscan.Responses;
using Nethereum.DataServices.Etherscan.Responses.Account;
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
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=txlist&address={address}&startblock={startBlock}&endblock{endBlock}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanGetAccountTransactionsResponse>>(url).ConfigureAwait(false);
        }

        public Task<EtherscanResponse<List<EtherscanGetAccountInternalTransactionsResponse>>> GetAccountInternalTransactionsAsync(string address, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            return GetAccountInternalTransactionsAsync(address, 0, BigInteger.Parse("999999999999999"), page, offset, sort);
        }

        public async Task<EtherscanResponse<List<EtherscanGetAccountInternalTransactionsResponse>>> GetAccountInternalTransactionsAsync(string address, BigInteger startBlock, BigInteger endBlock, int page = 1, int offset = 10, EtherscanResultSort sort = Etherscan.EtherscanResultSort.Ascending)
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=txlistinternal&address={address}&startblock={startBlock}&endblock{endBlock}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanGetAccountInternalTransactionsResponse>>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<string>> GetBalanceAsync(string address, string tag = "latest")
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=balance&address={address}&tag={tag}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<string>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanBalanceMultiResponse>>> GetBalancesAsync(string[] addresses, string tag = "latest")
        {
            var joined = string.Join(",", addresses);
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=balancemulti&address={joined}&tag={tag}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanBalanceMultiResponse>>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanTokenTransferResponse>>> GetTokenTransfersAsync(string address, string contractAddress = null, BigInteger? startBlock = null, BigInteger? endBlock = null, int page = 1, int offset = 100, EtherscanResultSort sort = EtherscanResultSort.Ascending)
        {
            var query = $"{EtherscanRequestService.BaseUrl}&module=account&action=tokentx&address={address}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            if (!string.IsNullOrEmpty(contractAddress)) query += $"&contractaress={contractAddress}";
            if (startBlock.HasValue) query += $"&startblock={startBlock}";
            if (endBlock.HasValue) query += $"&endblock={endBlock}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanTokenTransferResponse>>(query).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanNftTransferResponse>>> GetErc721TransfersAsync(string address, string contractAddress = null, BigInteger? startBlock = null, BigInteger? endBlock = null, int page = 1, int offset = 100, EtherscanResultSort sort = EtherscanResultSort.Ascending)
        {
            var query = $"{EtherscanRequestService.BaseUrl}&module=account&action=tokennfttx&address={address}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            if (!string.IsNullOrEmpty(contractAddress)) query += $"&contractaddress={contractAddress}";
            if (startBlock.HasValue) query += $"&startblock={startBlock}";
            if (endBlock.HasValue) query += $"&endblock={endBlock}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanNftTransferResponse>>(query).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanErc1155TransferResponse>>> GetErc1155TransfersAsync(string address, string contractAddress = null, BigInteger? startBlock = null, BigInteger? endBlock = null, int page = 1, int offset = 100, EtherscanResultSort sort = EtherscanResultSort.Ascending)
        {
            var query = $"{EtherscanRequestService.BaseUrl}&module=account&action=token1155tx&address={address}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            if (!string.IsNullOrEmpty(contractAddress)) query += $"&contractaddress={contractAddress}";
            if (startBlock.HasValue) query += $"&startblock={startBlock}";
            if (endBlock.HasValue) query += $"&endblock={endBlock}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanErc1155TransferResponse>>(query).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<EtherscanFundedByResponse>> GetFundedByAsync(string address)
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=fundedby&address={address}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<EtherscanFundedByResponse>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanMinedBlockResponse>>> GetMinedBlocksAsync(string address, string blockType = "blocks", int page = 1, int offset = 10)
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=getminedblocks&address={address}&blocktype={blockType}&page={page}&offset={offset}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanMinedBlockResponse>>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<List<EtherscanBeaconWithdrawalResponse>>> GetBeaconWithdrawalsAsync(string address, BigInteger? startBlock = null, BigInteger? endBlock = null, int page = 1, int offset = 100, EtherscanResultSort sort = EtherscanResultSort.Ascending)
        {
            var query = $"{EtherscanRequestService.BaseUrl}&module=account&action=txsBeaconWithdrawal&address={address}&page={page}&offset={offset}&sort={sort.ConvertToRequestFormattedString()}&apikey={EtherscanRequestService.ApiKey}";
            if (startBlock.HasValue) query += $"&startblock={startBlock}";
            if (endBlock.HasValue) query += $"&endblock={endBlock}";
            return await EtherscanRequestService.GetDataAsync<List<EtherscanBeaconWithdrawalResponse>>(query).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<string>> GetHistoricalBalanceAsync(string address, BigInteger blockNumber)
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=account&action=balancehistory&address={address}&blockno={blockNumber}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<string>(url).ConfigureAwait(false);
        }
    }
}
