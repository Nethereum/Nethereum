using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Util.Rest;

namespace Nethereum.Beaconchain.LightClient
{
    public class LightClientApiClient : ILightClientApi
    {
        private readonly IRestHttpHelper _restHelper;
        private readonly string _baseUrl;

        public LightClientApiClient(string baseUrl, IRestHttpHelper restHelper)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _restHelper = restHelper;
        }

        public async Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
        {
            var url = $"{_baseUrl}/eth/v1/beacon/light_client/bootstrap/{blockRoot}";
            return await _restHelper.GetAsync<LightClientBootstrapResponse>(url);
        }

        public async Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count)
        {
            var url = $"{_baseUrl}/eth/v1/beacon/light_client/updates?start_period={startPeriod}&count={count}";
            return await _restHelper.GetAsync<List<LightClientUpdateResponse>>(url);
        }

        public async Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
        {
            var url = $"{_baseUrl}/eth/v1/beacon/light_client/finality_update";
            return await _restHelper.GetAsync<LightClientFinalityUpdateResponse>(url);
        }

        public async Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync()
        {
            var url = $"{_baseUrl}/eth/v1/beacon/light_client/optimistic_update";
            return await _restHelper.GetAsync<LightClientOptimisticUpdateResponse>(url);
        }
    }
}
