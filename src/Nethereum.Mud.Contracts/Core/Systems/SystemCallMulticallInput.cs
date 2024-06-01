using Nethereum.Mud;
using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class SystemCallMulticallInput<TFunctionMessage, TSystemResource> : MulticallInput<TFunctionMessage>, ISystemCallMulticallInput where TFunctionMessage : FunctionMessage, new()
       where TSystemResource : SystemResource, new()
    {
        public IResource SystemResource => ResourceRegistry.GetResource<TSystemResource>();
        public SystemCallMulticallInput(TFunctionMessage functionMessage, string contractAddressTarget) : base(functionMessage, contractAddressTarget) { }

        public SystemCallData GetSystemCallData()
        {
            return new SystemCallData() { CallData = GetCallData(), SystemId = SystemResource.ResourceIdEncoded };
        }
    }

}



