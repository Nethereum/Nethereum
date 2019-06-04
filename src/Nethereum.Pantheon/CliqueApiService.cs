using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.Clique;
using Nethereum.RPC;

namespace Nethereum.Pantheon
{
    public class CliqueApiService : RpcClientWrapper, ICliqueApiService
    {
        public CliqueApiService(IClient client) : base(client)
        {
            Discard = new CliqueDiscard(client);
            GetSigners = new CliqueGetSigners(client);
            GetSignersAtHash = new CliqueGetSignersAtHash(client);
            Proposals = new CliqueProposals(client);
            Propose = new CliquePropose(client);

        }

        public ICliqueDiscard Discard { get; }
        public ICliqueGetSigners GetSigners { get; }
        public ICliqueGetSignersAtHash GetSignersAtHash { get; }
        public ICliqueProposals Proposals { get; }
        public ICliquePropose Propose { get; }
    }
}