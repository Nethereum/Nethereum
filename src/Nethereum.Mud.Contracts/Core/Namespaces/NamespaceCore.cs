using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Web3;

namespace Nethereum.Mud.Contracts.Core.Namespaces
{
    public class NamespaceCore<TNamespaceResource, TSystemServices, TTableServices> where TNamespaceResource : NamespaceResource
        where TSystemServices : SystemsServices
        where TTableServices : TablesServices
    {
        public IWeb3 Web3 { get; protected set; }
        public string ContractAddress { get; protected set; }

        public NamespaceCore(TNamespaceResource namespaceResource, IWeb3 web3, string contractAddress)
        {
            NamespaceResource = namespaceResource;
            ContractAddress = contractAddress;
            Web3 = web3;
        }

        public TNamespaceResource NamespaceResource { get; protected set; }

        public TSystemServices Systems { get; protected set; }
        public TTableServices Tables { get; protected set; }
    }
}
