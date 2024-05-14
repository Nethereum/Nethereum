using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World.Tables;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class SystemServiceResourceRegistration<TSystemResource, TService> : ISystemServiceResourceRegistration where TSystemResource : SystemResource, new()
        where TService : ContractWeb3ServiceBase, ISystemService<TSystemResource>
    {
        public TService Service { get; set; }
        public string WorldAddress { get; }

        protected SystemRegistrationBuilder systemRegistrationBuilder;

        protected RegistrationSystemService RegistrationSystemService { get; set; }
        protected BatchCallSystemService BatchCallSystemService { get; set; }

        public SystemServiceResourceRegistration(TService service, string worldAddress)
        {
            Service = service;
            WorldAddress = worldAddress;
            InitialiseServices();
            systemRegistrationBuilder = new SystemRegistrationBuilder();
        }

        private void InitialiseServices()
        {
            RegistrationSystemService = new RegistrationSystemService(Service.Web3, WorldAddress);
            BatchCallSystemService = new BatchCallSystemService(Service.Web3, WorldAddress);
        }

        public SystemServiceResourceRegistration(TService service, string worldAddress, SystemRegistrationBuilder systemRegistrationBuilder)
        {
            Service = service;
            WorldAddress = worldAddress;
            InitialiseServices();
            this.systemRegistrationBuilder = systemRegistrationBuilder;
        }

        public SystemServiceResourceRegistration(TService service)
        {
            Service = service;
            WorldAddress = service.ContractAddress;
            InitialiseServices();
            systemRegistrationBuilder = new SystemRegistrationBuilder();
        }



        protected TSystemResource systemResource;

        public TSystemResource Resource
        {
            get { if (systemResource == null) systemResource = new TSystemResource(); return systemResource; }
            set { systemResource = value; }
        }

        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            return systemRegistrationBuilder.CreateRegisterRootFunctionSelectors(Service, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        public List<SystemCallData> CreateRegisterRootFunctionSelectorsBatchSystemCallData(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            return systemRegistrationBuilder.CreateRegisterRootFunctionSelectorsBatchSystemCallData(Service, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        public RegisterSystemFunction GetRegisterSystemFunction(string deployedAddress, bool publicAccess = true)
        {
            return systemRegistrationBuilder.CreateRegisterSystemFunction(this, deployedAddress, publicAccess);
        }


        public Task<string> RegisterSystemAsync(string deployedAddress, bool publicAccess = true)
        {
            var registerFunction = GetRegisterSystemFunction(deployedAddress, publicAccess);
            return RegistrationSystemService.RegisterSystemRequestAsync(registerFunction);
        }

        public Task<string> BatchRegisterRootFunctionSelectorsRequestAsync(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            var callData = CreateRegisterRootFunctionSelectorsBatchSystemCallData(excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
            return BatchCallSystemService.BatchCallRequestAsync(callData);
        }

        public Task<string> BatchRegisterSystemAndRootFunctionSelectorsRequestAsync(string deployedAddress, bool publicAccess = true, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            List<SystemCallData> registerFunctionBatchCallData = CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(deployedAddress, publicAccess, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
            return BatchCallSystemService.BatchCallRequestAsync(registerFunctionBatchCallData);
        }

        public List<SystemCallData> CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(string deployedAddress, bool publicAccess, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords, bool excludeDefaultSystemFunctions)
        {
            var registerFunction = GetRegisterSystemFunction(deployedAddress, publicAccess);
            var registerFunctionBatchCallData = new List<SystemCallData> { registerFunction.CreateBatchSystemCallDataForFunction<RegistrationSystemResource, RegisterSystemFunction>() };
            var registerFunctionSelectors = CreateRegisterRootFunctionSelectorsBatchSystemCallData(excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
            registerFunctionBatchCallData.AddRange(registerFunctionSelectors);
            return registerFunctionBatchCallData;
        }
    }


}
