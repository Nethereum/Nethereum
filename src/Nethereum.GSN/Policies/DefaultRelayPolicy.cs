using Nethereum.GSN.Interfaces;
using Nethereum.GSN.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.GSN.Policies
{
    public class DefaultRelayPolicy : IRelayPolicy
    {
        public IEnumerable<RelayOnChain> Execute(IEnumerable<RelayOnChain> relays)
        {
            return relays.OrderBy(x => x.Fee);
        }

        public Task GraceAsync(RelayOnChain relay)
        {
            return Task.FromResult(0);
        }

        public Task PenalizeAsync(RelayOnChain relay)
        {
            return Task.FromResult(0);
        }
    }
}
