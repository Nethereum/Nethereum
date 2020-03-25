using Nethereum.GSN.Models;
using System.Threading.Tasks;

namespace Nethereum.GSN.Interfaces
{
    public interface IRelayPenaltyPolicy
    {
        Task PenalizeAsync(RelayOnChain relay);
    }
}
