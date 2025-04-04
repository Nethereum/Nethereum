using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EntryPointTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EntryPointTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task ShouldCreateAccountAfterPrefundAsync()
        {
            var salt = 20;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;
            var ethKey = new Nethereum.Signer.EthECKey(privateKey);

            var accountOwner = EthereumClientIntegrationFixture.AccountAddress;
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());
            
            var simpleAccountFactoryDeployment = new SimpleAccountFactoryDeployment();
            simpleAccountFactoryDeployment.EntryPoint = entryPointService.ContractAddress;
            
            var simpleAccountFactoryService =
                await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(web3, simpleAccountFactoryDeployment);

            var accountAddress = await simpleAccountFactoryService.GetAddressQueryAsync(accountOwner, salt);
            var initCode = simpleAccountFactoryService.GetCreateAccountInitCode(accountOwner, salt);

            await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(accountAddress, 1);

            var code = await web3.Eth.GetCode.SendRequestAsync(accountAddress);

            Assert.True(code == null || code.RemoveHexPrefix().Length == 0, "account exists before creation");

            var createOp = await entryPointService.SignAndInitialiseUserOperationAsync(new UserOperation()
            {
                InitCode = simpleAccountFactoryService.GetCreateAccountInitCode(accountOwner, salt),
                CallGasLimit = 1000000,
                VerificationGasLimit = 2000000
            }, ethKey);

            var handleOpsRequest = new HandleOpsFunction()
            {
                Ops = new List<PackedUserOperation>() { createOp },
                Beneficiary = accountOwner,
                Gas = 10000000
            };

            var receipt = await entryPointService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsRequest);

            var hash = await entryPointService.GetUserOpHashQueryAsync(createOp);

            var accountDeployed = receipt.Logs.DecodeAllEvents<AccountDeployedEventDTO>().FirstOrDefault();
            Assert.True(receipt.Status.Value == 1);
            Assert.NotNull(accountDeployed);
            Assert.True(hash.ToHex().IsTheSameHex(accountDeployed.Event.UserOpHash.ToHex()));
            Assert.True(accountDeployed.Event.Sender.IsTheSameAddress(createOp.Sender));
            Assert.True(accountDeployed.Event.Factory.IsTheSameAddress(createOp.InitCode.ToHex().Substring(0, 40)));

        }

    }
}
