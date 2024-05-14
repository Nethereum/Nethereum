using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.Core.Namespaces;

namespace Nethereum.Mud.Contracts.Store
{
    public class StoreNamespace : NamespaceBase<StoreNamespaceResource, EmptySystemsServices, StoreTableServices>
    {
        public StoreNamespace(IWeb3 web3, string contractAddress) : base(new StoreNamespaceResource())
        {
            Tables = new StoreTableServices(web3, contractAddress);
        }
    }
}
