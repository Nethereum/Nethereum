using System.Threading.Tasks;

namespace Nethereum.Consensus.LightClient
{
    public interface ILightClientStore
    {
        Task<LightClientState?> LoadAsync();
        Task SaveAsync(LightClientState state);
    }
}
