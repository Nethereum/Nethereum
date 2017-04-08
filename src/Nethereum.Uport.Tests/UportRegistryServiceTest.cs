using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.TestRPCRunner;
using Nethereum.Web3.TransactionReceipts;
using Xunit;

namespace Nethereum.Uport.Tests
{
    public class UportRegistryServiceTest
    {
        [Fact]
        public async void ShouldDeployAContractWithConstructor()
        {
            using (var testrpcRunner = new TestRPCEmbeddedRunner())
            {
                try
                {
                    testrpcRunner.RedirectOuputToDebugWindow = true;
                    testrpcRunner.StartTestRPC();

                    var web3 = new Web3.Web3();
                    var addressFrom = (await web3.Eth.Accounts.SendRequestAsync())[0];

                    var transactionService = new TransactionReceiptPollingService(web3);
                    var previousVersionAddress = "0x12890d2cce102216644c59dae5baed380d84830c";
                    var registrySevice = await UportRegistryService.DeployContractAndGetServiceAsync(transactionService,
                        web3,
                        addressFrom,
                        previousVersionAddress,
                        new HexBigInteger(4712388));

                    Assert.Equal(previousVersionAddress, await registrySevice.PreviousPublishedVersionAsyncCall());
                }
                finally
                {
                    testrpcRunner.StopTestRPC();
                }
            }
        }

        [Fact]
        public async void ShouldDeployAContractWithConstructor2()
        {
            using (var testrpcRunner = new TestRPCEmbeddedRunner())
            {
                try
                {
                    testrpcRunner.RedirectOuputToDebugWindow = true;
                    testrpcRunner.StartTestRPC();

                    var web3 = new Web3.Web3();
                    var addressFrom = (await web3.Eth.Accounts.SendRequestAsync())[0];
                    var transactionService = new TransactionReceiptPollingService(web3);
                    var previousVersionAddress = "0x12890d2cce102216644c59dae5baed380d84830c";
                    var contractAddress = await transactionService.DeployContractAndGetAddressAsync(() =>
                        UportRegistryService.DeployContractAsync(web3, addressFrom, previousVersionAddress, new HexBigInteger(4712388)
                            ));

                    var registrySevice = new UportRegistryService(web3, contractAddress);
                    Assert.Equal(previousVersionAddress, await registrySevice.PreviousPublishedVersionAsyncCall());
                }
                finally
                {
                    testrpcRunner.StopTestRPC();
                }

            }
        }
    }
}
