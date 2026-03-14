using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.ContractStorage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class StorageUtilTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public StorageUtilTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class StorageTestDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "6080806040523460135760dd908160188239f35b5f80fdfe60808060405260043610156011575f80fd5b5f3560e01c90816327e235e3146063575063e30443bc14602f575f80fd5b34605f576040366003190112605f576001600160a01b03604c6092565b165f525f60205260243560405f20555f80f35b5f80fd5b34605f576020366003190112605f576020906001600160a01b0360836092565b165f525f825260405f20548152f35b600435906001600160a01b0382168203605f5756fea2646970667358221220ebffd37981715b4cc6cdbbb16e0e132b8038d308e7de1304ba3838807a721c7464736f6c634300081c0033";

            public StorageTestDeployment() : base(BYTECODE) { }
        }

        [Function("setBalance")]
        public class SetBalanceFunction : FunctionMessage
        {
            [Parameter("address", "account", 1)]
            public string Account { get; set; }

            [Parameter("uint256", "amount", 2)]
            public BigInteger Amount { get; set; }
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "StorageUtil: read mapping value via raw storage slot", SkillName = "built-in-standards", Order = 8)]
        public async Task ShouldCalculateStorageKeyAndReadMappingValue()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StorageTestDeployment>()
                .SendRequestAndWaitForReceiptAsync(new StorageTestDeployment()).ConfigureAwait(false);

            var contractAddress = deploymentReceipt.ContractAddress;
            var testAddress = EthereumClientIntegrationFixture.AccountAddress;
            var expectedBalance = new BigInteger(42000);

            var contractHandler = web3.Eth.GetContractHandler(contractAddress);
            await contractHandler.SendRequestAndWaitForReceiptAsync(new SetBalanceFunction
            {
                Account = testAddress,
                Amount = expectedBalance
            }).ConfigureAwait(false);

            var storageKeyBigInt = StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(testAddress, 0);

            var storageValue = await web3.Eth.GetStorageAt
                .SendRequestAsync(contractAddress, new HexBigInteger(storageKeyBigInt)).ConfigureAwait(false);

            var actualBalance = storageValue.HexToBigInteger(false);
            Assert.Equal(expectedBalance, actualBalance);
        }
    }
}
