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

namespace Nethereum.Mud.Contracts.World.Systems.BalanceTransferSystem
{

    public class BalanceTransferSystemResource:SystemResource
    {
        public BalanceTransferSystemResource():base("BalanceTransfer", "world"){}
    }


    public partial class BalanceTransferSystemService : ISystemService<BalanceTransferSystemResource>
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
        {
            get
            {
                return this.GetSystemServiceResourceRegistration<BalanceTransferSystemResource, BalanceTransferSystemService>();
            }
        }

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }
    }




}
