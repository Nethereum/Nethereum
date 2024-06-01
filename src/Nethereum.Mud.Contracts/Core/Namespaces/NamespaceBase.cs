using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;

namespace Nethereum.Mud.Contracts.Core.Namespaces
{
    public class NamespaceBase<TNamespaceResource, TSystemServices, TTableServices> : NamespaceCore<TNamespaceResource, TSystemServices, TTableServices>
         where TNamespaceResource : NamespaceResource, new()
         where TSystemServices : SystemsServices
         where TTableServices : TablesServices
    {
        protected RegistrationSystemService RegistrationSystemService { get; set; }
        public WorldNamespace World { get; protected set; }
        public StoreNamespace Store { get; protected set; }
        public NamespaceBase(IWeb3 web3, string contractAddress) : base(new TNamespaceResource(), web3, contractAddress)
        {
            World = new WorldNamespace(web3, contractAddress);
            Store = new StoreNamespace(web3, contractAddress);
            RegistrationSystemService = new RegistrationSystemService(web3, contractAddress);   
        }

        public NamespaceBase(WorldNamespace worldNamespace, StoreNamespace storeNamespace) : base(new TNamespaceResource(), worldNamespace.Web3, worldNamespace.ContractAddress)
        {
            World = worldNamespace;
            Store = storeNamespace;
            RegistrationSystemService = new RegistrationSystemService(worldNamespace.Web3, worldNamespace.ContractAddress);
        }

        public async Task<string> RegisterNamespaceRequestAsync()
        {
            if (!await IsNamespaceRegistered())
            {
                return await RegistrationSystemService.RegisterNamespaceRequestAsync(NamespaceResource.ResourceIdEncoded);
            }
            return null;
        }

        public async Task<bool> IsNamespaceRegistered()
        {
            var resource = await Store.Tables.ResourceIdsTableService.GetTableRecordAsync(NamespaceResource.ResourceIdEncoded);
            return resource.Values.Exists;
        }

        public async Task<TransactionReceipt> RegisterNamespaceRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            if (!await IsNamespaceRegistered())
            {
                return await RegistrationSystemService.RegisterNamespaceRequestAndWaitForReceiptAsync(NamespaceResource.ResourceIdEncoded, cancellationToken);
            }

            return null;
        }

    }
}
