using Nethereum.ABI.Model;
using Nethereum.Contracts;
using System.Collections.Generic;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public interface ISystemService<TSystemResource>: ISystemService where TSystemResource : SystemResource
    {

    }


    public interface ISystemService
    {
        public List<FunctionABI> GetSystemFunctionABIs();
        public IResource Resource { get; }

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator { get; }
    }
}
