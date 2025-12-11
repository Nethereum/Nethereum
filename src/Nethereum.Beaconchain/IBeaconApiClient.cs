using Nethereum.Beaconchain.LightClient;

namespace Nethereum.Beaconchain
{
    public interface IBeaconApiClient
    {
        ILightClientApi LightClient { get; }
        string BaseUrl { get; }
    }
}
