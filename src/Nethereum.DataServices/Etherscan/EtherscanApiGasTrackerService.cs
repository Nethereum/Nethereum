using Nethereum.DataServices.Etherscan.Responses;
using Nethereum.DataServices.Etherscan.Responses.GasTracker;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Etherscan
{
    public class EtherscanApiGasTrackerService
    {
        public EtherscanRequestService EtherscanRequestService { get; private set; }

        public EtherscanApiGasTrackerService(EtherscanRequestService etherscanRequestService)
        {
            EtherscanRequestService = etherscanRequestService;
        }

        public async Task<EtherscanResponse<string>> GetEstimatedConfirmationTimeAsync(BigInteger gasPriceInWei)
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=gastracker&action=gasestimate&gasprice={gasPriceInWei}&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<string>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<EtherscanGasOracleResponse>> GetGasOracleAsync()
        {
            var url = $"{EtherscanRequestService.BaseUrl}&module=gastracker&action=gasoracle&apikey={EtherscanRequestService.ApiKey}";
            return await EtherscanRequestService.GetDataAsync<EtherscanGasOracleResponse>(url).ConfigureAwait(false);
        }
    }
}
