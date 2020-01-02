using Nethereum.GSN.Models;
using System.Threading.Tasks;

namespace Nethereum.GSN.Policies
{
    public interface IRelayGracePolicy
    {
        Task GraceAsync(RelayOnChain relay);
    }
}
