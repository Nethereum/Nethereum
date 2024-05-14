using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Contracts;
using System.Threading.Tasks;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition;
using Nethereum.Mud.Contracts.Core.Systems;

namespace Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem
{

    public class AccessManagementSystemResource:SystemResource
    {
        public AccessManagementSystemResource():base("AccessManagement", "world"){}
    }


    public partial class AccessManagementSystemService : ISystemService<AccessManagementSystemResource>
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator 
        {
            get
            {
               return this.GetSystemServiceResourceRegistration<AccessManagementSystemResource, AccessManagementSystemService>();
            }
         }

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }
    }


}
