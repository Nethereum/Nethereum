using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.IntegrationTests.MudTest.Systems.IncrementSystem;
using Nethereum.Web3;

namespace Nethereum.Mud.IntegrationTests.MudTest
{
    public class MudTestSystemServices : SystemsServices
    {
        public IncrementSystemService IncrementSystemService { get; protected set; }
        public MudTestSystemServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            IncrementSystemService = new IncrementSystemService(web3, contractAddress);
            SystemServices = new List<ISystemService> { IncrementSystemService };
        }
    }
}
