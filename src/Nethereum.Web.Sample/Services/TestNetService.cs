using Nethereum.Web.Sample.Model;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Web.Sample.Services
{
    public class TestNetService
    {
        private List<TestNet> testNets;

        public TestNetService()
        {
            testNets = new List<TestNet>
            {
                new TestNet {Id = 1, DisplayName = "Local TestNet", Address = "http://localhost:8545/" },
                new TestNet {Id = 2, DisplayName = "Ropsten/Infura", Address = "https://ropsten.infura.io/" },
            };
        }

        public List<TestNet> AllTestNets()
        {
            return testNets;
        }

        public string EndpointAddress(int networkId)
        {
            return testNets.First(x => x.Id == networkId).Address;
        }
    }
}