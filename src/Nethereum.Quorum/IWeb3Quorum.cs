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

        IPermissionService Permission { get;}
        IPrivacyService Privacy { get; }
        IRaftService Raft { get;  }
        IIBFTService IBFT { get; }

        IContractExtensionsService ContractExtensions { get;}
        IDebugQuorumService DebugQuorum { get; }

        void ClearPrivateForRequestParameters();
        void SetPrivateRequestParameters(IEnumerable<string> privateFor, string privateFrom = null);
    }
}