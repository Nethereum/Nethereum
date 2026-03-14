using System;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
   
        public class ChainlistRpcApiServiceTests
        {
            [NethereumDocExample(DocSection.DataServices, "chainlist-rpc", "Chainlist RPC chain discovery", Order = 1)]
            [Fact]
            public async Task ShouldGetAllChains()
            {
                var service = new Chainlist.ChainlistRpcApiService();
                var chains = await service.GetAllChainsAsync();

                Assert.NotNull(chains);
                Assert.NotEmpty(chains);
                Assert.Contains(chains, c => c.ChainId == 1); // Ethereum Mainnet present
            }

            [NethereumDocExample(DocSection.DataServices, "chainlist-rpc", "Chainlist Ethereum mainnet RPC URLs", Order = 2)]
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
