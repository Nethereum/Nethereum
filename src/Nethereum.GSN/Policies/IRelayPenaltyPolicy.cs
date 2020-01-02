using Nethereum.GSN.Models;
using System.Threading.Tasks;

namespace Nethereum.GSN.Policies
{
    public interface IRelayPenaltyPolicy
    {
        Task PenalizeAsync(RelayOnChain relay);
    }
}
