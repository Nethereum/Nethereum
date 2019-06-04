using Nethereum.Pantheon.RPC.Clique;

namespace Nethereum.Pantheon
{
    public interface ICliqueApiService
    {
        ICliqueDiscard Discard { get; }
        ICliqueGetSigners GetSigners { get; }
        ICliqueGetSignersAtHash GetSignersAtHash { get; }
        ICliqueProposals Proposals { get; }
        ICliquePropose Propose { get; }
    }
}