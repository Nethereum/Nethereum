using System.Collections.Generic;
using Nethereum.Quorum.RPC.Services;
using Nethereum.Web3;

namespace Nethereum.Quorum
{
    public interface IWeb3Quorum: IWeb3
    {
        List<string> PrivateFor { get; }
        string PrivateFrom { get; }
        IQuorumChainService Quorum { get; }

        void ClearPrivateForRequestParameters();
        void SetPrivateRequestParameters(List<string> privateFor, string privateFrom = null);
    }
}