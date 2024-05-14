using Nethereum.Contracts;
using Nethereum.KeyStore.Model;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using System.Collections.Generic;

namespace Nethereum.Mud.Contracts.World.Systems.BatchCallSystem
{
    public static class BatchCallExtensions
    {
        public static BatchCallFunction CreateBatchCallFunction<TSystemResource>(this List<IMulticallInput> functionMessages)
            where TSystemResource : SystemResource, new()
        {

            var systemCalls = functionMessages.CreateBatchSystemCallData<TSystemResource>();
            return new BatchCallFunction()
            {
                SystemCalls = systemCalls,
            };
        }

        public static List<SystemCallData> CreateBatchSystemCallData<TSystemResource, TFunctionMessage>(this List<TFunctionMessage> functionMessages)
            where TSystemResource : SystemResource, new()
            where TFunctionMessage : FunctionMessage, new()
        {
            var list = new List<SystemCallData>();
            var systemResource = ResourceRegistry.GetResourceEncoded<TSystemResource>();

            foreach (var functionMessage in functionMessages)
            {
                list.Add(new SystemCallData()
                {
                    CallData = functionMessage.GetCallData(),
                    SystemId = systemResource
                });
            }
            return list;
        }

        public static SystemCallData CreateBatchSystemCallDataForFunction<TSystemResource, TFunctionMessage>(this TFunctionMessage functionMessage)
          where TSystemResource : SystemResource, new()
          where TFunctionMessage : FunctionMessage, new()
        {
            var systemResource = ResourceRegistry.GetResourceEncoded<TSystemResource>();
            return new SystemCallData()
            {
                CallData = functionMessage.GetCallData(),
                SystemId = systemResource
            };
        }

        public static List<SystemCallData> CreateBatchSystemCallData<TSystemResource>(this List<IMulticallInput> functionMessages)
        where TSystemResource : SystemResource, new()
        {
            var list = new List<SystemCallData>();
            var systemResource = ResourceRegistry.GetResourceEncoded<TSystemResource>();

            foreach (var functionMessage in functionMessages)
            {
                list.Add(new SystemCallData()
                {
                    CallData = functionMessage.GetCallData(),
                    SystemId = systemResource
                });
            }
            return list;
        }
    }
}
