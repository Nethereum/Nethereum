using System.Threading.Tasks;

namespace Nethereum.Consensus.LightClient
{
    /// <summary>
    /// Convenience store that keeps the state in memory (useful for tests or prototypes).
    /// </summary>
    public class InMemoryLightClientStore : ILightClientStore
    {
        private LightClientState? _state;

        public Task<LightClientState?> LoadAsync() => Task.FromResult(_state);

        public Task SaveAsync(LightClientState state)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }
}
