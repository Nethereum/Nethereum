using System.Collections.Generic;
using Nethereum.Geth;
using Nethereum.Quorum.RPC.Services;
using Nethereum.Web3;

namespace Nethereum.Quorum
{
    public interface IWeb3Quorum: IWeb3Geth
    {
        List<string> PrivateFor { get; }
        string PrivateFrom { get; }
        IQuorumChainService Quorum { get; }

        void ClearPrivateForRequestParameters();
        void SetPrivateRequestParameters(IEnumerable<string> privateFor, string privateFrom = null);
    }
}