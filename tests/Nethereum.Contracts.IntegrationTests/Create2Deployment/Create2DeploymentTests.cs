using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Create2Deployment
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class Create2DeploymentTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public Create2DeploymentTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }


        [Fact]
        public async Task ShouldDeployDeterministicDeployer()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deployment = await web3.Eth.Create2DeterministicDeploymentProxyService.GenerateEIP155Create2DeterministicDeploymentProxyDeploymentForCurrentChainAsync();
            var existDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deployment.Address);
            if(!existDeployer)
            {
                var addressDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(deployment);
                Assert.True(addressDeployer.IsTheSameAddress(deployment.Address));

                var existDeployerAfter = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deployment.Address);
                Assert.True(existDeployerAfter);
            }
        }

        [Fact]
        public async Task ShouldDeployCreate2UsingDeterministicDeployer()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var salt = "0x1234567890123456789012345678901234567890123456789012345678901234";
            var create2DeterministicDeploymentProxyService = web3.Eth.Create2DeterministicDeploymentProxyService;

            var deployment = await create2DeterministicDeploymentProxyService.GenerateEIP155Create2DeterministicDeploymentProxyDeploymentForCurrentChainAsync();
            var addressDeployer = await create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(deployment);
            
            var contractByteCode =
               "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            var address = create2DeterministicDeploymentProxyService.CalculateCreate2Address(deployment.Address, salt, contractByteCode);
            var receipt = await create2DeterministicDeploymentProxyService.DeployContractRequestAndWaitForReceiptAsync(deployment.Address, salt, contractByteCode);
            Assert.True(await create2DeterministicDeploymentProxyService.CheckContractAlreadyDeployedAsync(address));

            var abi =
              @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";
            var contract = web3.Eth.GetContract(abi, address);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

        }

    }

}
