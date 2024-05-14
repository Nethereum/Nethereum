using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Mud.Contracts.Core.Tables;

namespace Nethereum.Mud.Contracts.Core.Namespaces
{
    public class UserNamespace<TNamespaceResource, TSystemServices, TTableServices> : NamespaceBase<TNamespaceResource, TSystemServices, TTableServices>
         where TNamespaceResource : NamespaceResource, new()
         where TSystemServices : SystemsServices
         where TTableServices : TablesServices
    {
        public WorldNamespace World { get; protected set; }
        public StoreNamespace Store { get; protected set; }
        public UserNamespace(IWeb3 web3, string contractAddress) : base(new TNamespaceResource())
        {
            World = new WorldNamespace(web3, contractAddress);
            Store = new StoreNamespace(web3, contractAddress);
        }

    }
}
