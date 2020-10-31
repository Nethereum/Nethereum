using Nethereum.GSN.Models;
using System.Threading.Tasks;

namespace Nethereum.GSN.Interfaces
{
    public interface IRelayGracePolicy
    {
        Task GraceAsync(RelayOnChain relay);
    }
}
