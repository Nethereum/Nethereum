using Nethereum.ABI.Decoders;
using Nethereum.Mud.Contracts.ContractHandlers;
using Nethereum.Web3;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public static class SystemResourceExtensions
    {
        public static TSystemResource GetResource<TSystemResource>(this ISystemService<TSystemResource> service) where TSystemResource : SystemResource, new()
        {
            return ResourceRegistry.GetResource<TSystemResource>();
        }

        public static SystemServiceResourceRegistration<TSystemResource, TService> GetSystemServiceResourceRegistration<TSystemResource, TService>(this TService service) where TSystemResource : SystemResource, new() where TService : ContractWeb3ServiceBase, ISystemService<TSystemResource>
        {
            return new SystemServiceResourceRegistration<TSystemResource, TService>(service);
        }

        public static void SetSystemCallFromDelegatorContractHandler(this ISystemService service, string delegatorAddress)
        {
           var handler =  MudCallFromContractHandler.CreateFromExistingContractService(service, delegatorAddress);
           service.ContractHandler = handler;
        }
    }
}
