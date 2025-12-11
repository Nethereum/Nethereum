using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient.Responses;

namespace Nethereum.Beaconchain.LightClient
{
    public interface ILightClientApi
    {
        Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot);
        Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count);
        Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync();
        Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync();
    }
}
