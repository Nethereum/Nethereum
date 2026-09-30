using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Beaconchain.Responses;

namespace Nethereum.Beaconchain
{
    public interface IBeaconApiClient
    {
        ILightClientApi LightClient { get; }
        string BaseUrl { get; }
        Task<StateForkResponse> GetStateForkAsync(string stateId = "head");
        Task<BlobSidecarResponse> GetBlobSidecarsAsync(string blockId = "head", int[] indices = null);
    }
}
