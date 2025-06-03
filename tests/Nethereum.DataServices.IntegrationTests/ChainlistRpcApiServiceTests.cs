using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
   
        public class ChainlistRpcApiServiceTests
        {
            [Fact]
            public async Task ShouldGetAllChains()
            {
                var service = new Chainlist.ChainlistRpcApiService();
                var chains = await service.GetAllChainsAsync();

                Assert.NotNull(chains);
                Assert.NotEmpty(chains);
                Assert.Contains(chains, c => c.ChainId == 1); // Ethereum Mainnet present
            }

            [Fact]
            public async Task ShouldContainEthereumMainnetWithRpcs()
            {
                var service = new Chainlist.ChainlistRpcApiService();
                var chains = await service.GetAllChainsAsync();

                var ethMainnet = chains.Find(c => c.ChainId == 1);
                Assert.NotNull(ethMainnet);
                Assert.NotEmpty(ethMainnet.Rpc);
                Assert.Contains(ethMainnet.Rpc, r => r.Url.Contains("https://"));
            }
        }
    

}
