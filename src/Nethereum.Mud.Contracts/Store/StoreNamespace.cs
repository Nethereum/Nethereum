using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.Core.Namespaces;
using Nethereum.Mud.Contracts.Core.StoreEvents;

namespace Nethereum.Mud.Contracts.Store
{
    public class StoreNamespace : NamespaceCore<StoreNamespaceResource, EmptySystemsServices, StoreTableServices>
    {
        public StoreNamespace(IWeb3 web3, string contractAddress) : base(new StoreNamespaceResource(), web3, contractAddress)
        {
            Tables = new StoreTableServices(web3, contractAddress);
            StoreEventsLogProcessingService = new StoreEventsLogProcessingService(web3, contractAddress);
        }

        public StoreEventsLogProcessingService StoreEventsLogProcessingService { get; protected set; }
    }
}
