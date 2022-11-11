using System.Threading.Tasks;

namespace Nethereum.GSN.Interfaces
{
    public interface IRelayQuery
    {
        Task<RelayCollection> GetAsync(string hubAddress, IRelayPriorityPolicy policy);
    }
}