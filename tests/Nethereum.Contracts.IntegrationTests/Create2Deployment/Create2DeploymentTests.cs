using Nethereum.Contracts.IntegrationTests.CQS;
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
            var deployment = await web3.Eth.Create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentUsingPreconfiguredSignatureAsync();
            var existDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deployment.Address);
            if (!existDeployer)
            {
                var addressDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(deployment);
                Assert.True(addressDeployer.IsTheSameAddress(deployment.Address));

                var existDeployerAfter = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deployment.Address);
                Assert.True(existDeployerAfter);
            }
        }

        [Fact]
        public async Task ShouldDeployDeterministicDeployerUsingACustomSigner()
        {
            var privateKeyCustomSigner = "541dbf545002f8832bcabbe05dd5dd86ee11a3f21ea6711b2ed192afc103fa41";
            var accountSignerDeployerCurrentChain = new Nethereum.Web3.Accounts.Account(privateKeyCustomSigner, EthereumClientIntegrationFixture.ChainId);
            var web3SignerDeployerCurrentChain = new Web3.Web3(accountSignerDeployerCurrentChain);
            var deploymentProxyCurrentChain = await web3SignerDeployerCurrentChain.Eth.Create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentAsync();

            var accountSignerDeployerDifferentChain = new Nethereum.Web3.Accounts.Account(privateKeyCustomSigner, 1);
            var web3SignerDeployerDifferentChain = new Web3.Web3(accountSignerDeployerDifferentChain);
            var deploymentProxyDifferentChain = await web3SignerDeployerDifferentChain.Eth.Create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentAsync();

            Assert.True(deploymentProxyCurrentChain.Address.IsTheSameAddress(deploymentProxyDifferentChain.Address));
            Assert.True(deploymentProxyCurrentChain.SignerAddress.IsTheSameAddress(deploymentProxyDifferentChain.SignerAddress));

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var existDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deploymentProxyCurrentChain.Address);
            if (!existDeployer)
            {
                var addressDeployer = await web3.Eth.Create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(deploymentProxyCurrentChain);
                Assert.True(addressDeployer.IsTheSameAddress(deploymentProxyCurrentChain.Address));

                var existDeployerAfter = await web3.Eth.Create2DeterministicDeploymentProxyService.HasProxyBeenDeployedAsync(deploymentProxyCurrentChain.Address);
                Assert.True(existDeployerAfter);
            }
        }



        [Fact]
        public async Task ShouldDeployCreate2UsingDeterministicDeployer()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            //var web3 = new Web3.Web3(new Nethereum.Web3.Accounts.Account(EthereumClientIntegrationFixture.AccountPrivateKey));
            var salt = "0x1234567890123456789012345678901234567890123456789012345678901234";
            var create2DeterministicDeploymentProxyService = web3.Eth.Create2DeterministicDeploymentProxyService;

            var deployment = await create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentUsingPreconfiguredSignatureAsync();
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


        [Fact]
        public async Task ShouldDeployCreate2UsingDeterministicDeployerUsingTypedDeployment()
        {
            var privateKeyCustomSigner = "541dbf545002f8832bcabbe05dd5dd86ee11a3f21ea6711b2ed192afc103fa41";
            var accountSignerDeployerCurrentChain = new Nethereum.Web3.Accounts.Account(privateKeyCustomSigner, EthereumClientIntegrationFixture.ChainId);
            var web3SignerDeployerCurrentChain = new Web3.Web3(accountSignerDeployerCurrentChain);
            var deploymentProxyCurrentChain = await web3SignerDeployerCurrentChain.Eth.Create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentAsync();


            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            //var web3 = new Web3.Web3(new Nethereum.Web3.Accounts.Account(EthereumClientIntegrationFixture.AccountPrivateKey));
            var salt = "0x1234567890123456789012345678901234567890123456789012345678901234";
            var create2DeterministicDeploymentProxyService = web3.Eth.Create2DeterministicDeploymentProxyService;
            var addressDeployer = await create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(deploymentProxyCurrentChain);

            var deploymentMessage = new StandardTokenDeployment
            {
                TotalSupply = 10000
            };

            var address = create2DeterministicDeploymentProxyService.CalculateCreate2Address(deploymentMessage, deploymentProxyCurrentChain.Address, salt);
            var receipt = await create2DeterministicDeploymentProxyService.DeployContractRequestAndWaitForReceiptAsync(deploymentMessage, deploymentProxyCurrentChain.Address, salt);
            Assert.True(await create2DeterministicDeploymentProxyService.CheckContractAlreadyDeployedAsync(address));

            //the "owner" of the contract is the deployment proxy so the total supply is allocated to it
            var balance = await web3.Eth.ERC20.GetContractService(address).BalanceOfQueryAsync(deploymentProxyCurrentChain.Address);
            Assert.Equal(10000, balance);

        }
    }

}
