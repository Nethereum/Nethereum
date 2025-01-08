using Nethereum.Mud;
using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class SystemCallMulticallInput<TFunctionMessage, TSystemResource> : MulticallInput<TFunctionMessage>, ISystemCallMulticallInput where TFunctionMessage : FunctionMessage, new()
       where TSystemResource : SystemResource, new()
    {
        public IResource SystemResource => ResourceRegistry.GetResource<TSystemResource>();
        public SystemCallMulticallInput(TFunctionMessage functionMessage) : base(functionMessage, null) { }

        public SystemCallData GetSystemCallData()
        {
            return new SystemCallData() { CallData = GetCallData(), SystemId = SystemResource.ResourceIdEncoded };
        }

        public SystemCallFromData GetSystemCallFromData(string from)
        {
            return new SystemCallFromData() { CallData = GetCallData(), SystemId = SystemResource.ResourceIdEncoded, From = from };
        }

        public SystemCallData GetSystemCallDataWithoutMudNamespacePrefix()
        {
            return new SystemCallData() { CallData = Input.GetCallDataWithoutMudNamespaceSignaturePrefix(), SystemId = SystemResource.ResourceIdEncoded };
        }

        public SystemCallFromData GetSystemCallFromDataWithoutMudNamespacePrefix(string from)
        {
            return new SystemCallFromData() { CallData = Input.GetCallDataWithoutMudNamespaceSignaturePrefix(), SystemId = SystemResource.ResourceIdEncoded, From = from };
        }
    }

}



