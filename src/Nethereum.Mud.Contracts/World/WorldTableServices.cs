using Nethereum.Web3;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Mud.Contracts.Store.Tables;

namespace Nethereum.Mud.Contracts.World
{
    public class WorldTableServices : TablesServices
    {
        public WorldTableServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            BalancesTableService = new BalancesTableService(web3, contractAddress);
            FunctionSelectorsTableService = new FunctionSelectorsTableService(web3, contractAddress);
            FunctionSignaturesTableService = new FunctionSignaturesTableService(web3, contractAddress);
            InitModuleAddressTableService = new InitModuleAddressTableService(web3, contractAddress);
            InstalledModulesTableService = new InstalledModulesTableService(web3, contractAddress);
            NamespaceDelegationControlTableService = new NamespaceDelegationControlTableService(web3, contractAddress);
            NamespaceOwnerTableService = new NamespaceOwnerTableService(web3, contractAddress);
            ResourceAccessTableService = new ResourceAccessTableService(web3, contractAddress);
            SystemHooksTableService = new SystemHooksTableService(web3, contractAddress);
            SystemRegistryTableService = new SystemRegistryTableService(web3, contractAddress);
            SystemsTableService = new SystemsTableService(web3, contractAddress);
            UserDelegationControlTableService = new UserDelegationControlTableService(web3, contractAddress);
            TableServices = new List<ITableServiceBase> { 
                BalancesTableService, 
                FunctionSelectorsTableService, 
                FunctionSignaturesTableService, 
                InitModuleAddressTableService, 
                InstalledModulesTableService, 
                NamespaceDelegationControlTableService, 
                NamespaceOwnerTableService, 
                ResourceAccessTableService, 
                SystemHooksTableService, 
                SystemRegistryTableService, 
                SystemsTableService, 
                UserDelegationControlTableService };
        }

        public BalancesTableService BalancesTableService { get; protected set; }
        public FunctionSelectorsTableService FunctionSelectorsTableService { get; protected set; }
        public FunctionSignaturesTableService   FunctionSignaturesTableService { get; protected set; }
        public InitModuleAddressTableService    InitModuleAddressTableService { get; protected set; }
        public InstalledModulesTableService InstalledModulesTableService { get; protected set; }
        public NamespaceDelegationControlTableService NamespaceDelegationControlTableService { get; protected set; }
        public NamespaceOwnerTableService NamespaceOwnerTableService { get; protected set; }
        public ResourceAccessTableService ResourceAccessTableService { get; protected set; }
        public SystemHooksTableService SystemHooksTableService { get; protected set; }
        public SystemRegistryTableService SystemRegistryTableService { get; protected set; }
        public SystemsTableService SystemsTableService { get; protected set; }
        public UserDelegationControlTableService UserDelegationControlTableService { get; protected set; }  

    }
}
