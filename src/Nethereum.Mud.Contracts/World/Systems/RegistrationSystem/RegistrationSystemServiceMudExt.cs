using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.ABI.Model;
using Nethereum.Mud.Contracts.Core.Systems;

namespace Nethereum.Mud.Contracts.World.Systems.RegistrationSystem
{
    public class RegistrationSystemResource: SystemResource
    {
        public RegistrationSystemResource() : base("Registration", "world") { }
    }

    public partial class RegistrationSystemService : ISystemService<RegistrationSystemResource>
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
        {
            get
            {
                return this.GetSystemServiceResourceRegistration<RegistrationSystemResource, RegistrationSystemService>();
            }
        }

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }
    }
}
