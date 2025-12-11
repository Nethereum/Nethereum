using System.Net.Http;
using Nethereum.Beaconchain.LightClient;
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
    }
}
