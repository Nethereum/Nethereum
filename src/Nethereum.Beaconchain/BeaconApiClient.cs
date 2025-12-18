using System.Net.Http;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Util.Rest;

namespace Nethereum.Beaconchain
{
    public class BeaconApiClient : IBeaconApiClient
    {
        private readonly IRestHttpHelper _restHelper;
        private readonly ILightClientApi _lightClient;

        public string BaseUrl { get; }

        public BeaconApiClient(string baseUrl, HttpClient httpClient = null)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            _restHelper = new RestHttpHelper(httpClient ?? new HttpClient());
            _lightClient = new LightClientApiClient(BaseUrl, _restHelper);
        }

        public BeaconApiClient(string baseUrl, IRestHttpHelper restHelper)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            _restHelper = restHelper;
            _lightClient = new LightClientApiClient(BaseUrl, _restHelper);
        }

        public ILightClientApi LightClient => _lightClient;

        public async Task<StateForkResponse> GetStateForkAsync(string stateId = "head")
        {
            var url = $"{BaseUrl}/eth/v1/beacon/states/{stateId}/fork";
            return await _restHelper.GetAsync<StateForkResponse>(url).ConfigureAwait(false);
        }
    }
}
