using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.Core.Tables;

namespace Nethereum.Mud.Contracts.Core.Namespaces
{
    public class NamespaceBase<TNamespaceResource, TSystemServices, TTableServices> where TNamespaceResource : NamespaceResource
        where TSystemServices : SystemsServices
        where TTableServices : TablesServices
    {
        public NamespaceBase(TNamespaceResource namespaceResource)
        {
            NamespaceResource = namespaceResource;
        }

        public TNamespaceResource NamespaceResource { get; protected set; }

        public TSystemServices Systems { get; protected set; }
        public TTableServices Tables { get; protected set; }
    }
}
