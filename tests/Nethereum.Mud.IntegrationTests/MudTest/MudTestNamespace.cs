using Nethereum.Mud.Contracts.Core.Namespaces;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Mud.IntegrationTests.MudTest
{
    public class MudTestNamespaceResource : NamespaceResource
    {
        public MudTestNamespaceResource() : base(null)
        {
        }
    }
    public class MudTestNamespace: NamespaceBase<MudTestNamespaceResource, MudTestSystemServices, MudTestTableServices>
    {
        public MudTestNamespace(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            Tables = new MudTestTableServices(web3, contractAddress);
            Systems = new MudTestSystemServices(web3, contractAddress);
        }
    }
}
