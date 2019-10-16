using Nethereum.GSN.Models;
using System.Collections.Generic;

namespace Nethereum.GSN.Policies
{
    public interface IRelayPriorityPolicy
    {
        IEnumerable<RelayOnChain> Execute(IEnumerable<RelayOnChain> relays);
    }
}
