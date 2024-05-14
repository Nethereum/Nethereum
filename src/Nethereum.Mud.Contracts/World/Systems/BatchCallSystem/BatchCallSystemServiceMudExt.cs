using Nethereum.Contracts;
using Nethereum.Mud.Contracts.Core.Systems;
using System.Collections.Generic;
using Nethereum.ABI.Model;  

namespace Nethereum.Mud.Contracts.World.Systems.BatchCallSystem
{
    public class BatchCallSystemResource : SystemResource
    {
        public BatchCallSystemResource() : base("BatchCall", "world") { }
    }

    public partial class BatchCallSystemService : ISystemService<BatchCallSystemResource>
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
        {
            get
            {
                return this.GetSystemServiceResourceRegistration<BatchCallSystemResource, BatchCallSystemService>();
            }
        }

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }
    }

    
}
