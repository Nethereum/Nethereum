using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Consensus.Clique
{
    public interface ICliqueProposalStore
    {
        void SetProposal(string target, bool authorize);
        void Discard(string target);
        IReadOnlyDictionary<string, bool> Proposals { get; }
    }

    public sealed class InMemoryCliqueProposalStore : ICliqueProposalStore
    {
        private readonly Dictionary<string, bool> _proposals = new();
        private readonly object _lock = new();

        public void SetProposal(string target, bool authorize)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddressLowerCase(target);
            lock (_lock)
            {
                _proposals[normalized] = authorize;
            }
        }

        public void Discard(string target)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddressLowerCase(target);
            lock (_lock)
            {
                _proposals.Remove(normalized);
            }
        }

        public IReadOnlyDictionary<string, bool> Proposals
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, bool>(_proposals);
                }
            }
        }
    }
}
