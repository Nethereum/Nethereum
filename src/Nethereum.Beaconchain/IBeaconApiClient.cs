using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;

namespace Nethereum.Beaconchain
{
    public interface IBeaconApiClient
    {
        ILightClientApi LightClient { get; }
        string BaseUrl { get; }
        Task<StateForkResponse> GetStateForkAsync(string stateId = "head");
    }
}
