using Nethereum.Web3;
using Nethereum.Mud.Contracts.Store.Tables;
using Nethereum.Mud.Contracts.Core.Tables;

namespace Nethereum.Mud.Contracts.Store
{
    public class StoreTableServices : TablesServices
    {
        public StoreTableServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            ResourceIdsTableService = new ResourceIdsTableService(web3, contractAddress);
            StoreHooksTableService = new StoreHooksTableService(web3, contractAddress);
            TablesTableService = new TablesTableService(web3, contractAddress);
            TableServices = new System.Collections.Generic.List<ITableServiceBase> { ResourceIdsTableService, StoreHooksTableService, TablesTableService };
        }
        public ResourceIdsTableService ResourceIdsTableService { get; protected set; }
        public StoreHooksTableService StoreHooksTableService { get; protected set; }
        public TablesTableService TablesTableService { get; protected set; }
    }
}
