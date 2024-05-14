using Nethereum.Web3;
using System.Collections.Generic;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class EmptySystemsServices : SystemsServices
    {
        public EmptySystemsServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            SystemServices = new List<ISystemService>();
        }
    }
}
