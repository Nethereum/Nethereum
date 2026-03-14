using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts.Standards
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ERC165Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ERC165Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class ERC165TestDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "6080806040523460135760d7908160188239f35b5f80fdfe60808060405260043610156011575f80fd5b5f3560e01c90816301ffc9a7146053575063b5821d2a14602f575f80fd5b34604f575f366003190112604f5760405163deadbeef60e01b8152602090f35b5f80fd5b34604f576020366003190112604f576004359063ffffffff60e01b8216809203604f5760209163deadbeef60e01b81149081156091575b5015158152f35b6301ffc9a760e01b14905083608a56fea26469706673582212207a3861370c1d99793cbc060155d89f8329ed07b6eb3889b4090c4c6a20f9d4b964736f6c634300081c0033";

            public ERC165TestDeployment() : base(BYTECODE) { }
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ERC-165 interface detection: check supported interfaces", SkillName = "built-in-standards", Order = 5)]
        public async Task ShouldDetectSupportedInterfaces()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<ERC165TestDeployment>()
                .SendRequestAndWaitForReceiptAsync(new ERC165TestDeployment()).ConfigureAwait(false);

            var contractAddress = deploymentReceipt.ContractAddress;
            var erc165 = web3.Eth.ERC165.GetContractService(contractAddress);

            var supportsErc165 = await erc165
                .SupportsInterfaceQueryAsync("0x01ffc9a7").ConfigureAwait(false);
            Assert.True(supportsErc165);

            var supportsCustom = await erc165
                .SupportsInterfaceQueryAsync("0xdeadbeef").ConfigureAwait(false);
            Assert.True(supportsCustom);

            var supportsErc721 = await erc165
                .SupportsInterfaceQueryAsync("0x80ac58cd").ConfigureAwait(false);
            Assert.False(supportsErc721);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ERC-165 on deployed ERC-721 contract", SkillName = "built-in-standards", Order = 6)]
        public async Task ShouldDetectERC721Interface()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            web3.Eth.TransactionManager.UseLegacyAsDefault = true;

            var erc721Deployment = new ERC721Tests.MyERC721Deployment() { Name = "Test", Symbol = "TST" };
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<ERC721Tests.MyERC721Deployment>()
                .SendRequestAndWaitForReceiptAsync(erc721Deployment).ConfigureAwait(false);

            var contractAddress = deploymentReceipt.ContractAddress;
            var erc165 = web3.Eth.ERC165.GetContractService(contractAddress);

            var supportsErc721 = await erc165
                .SupportsInterfaceQueryAsync("0x80ac58cd").ConfigureAwait(false);
            Assert.True(supportsErc721);

            var supportsErc721Metadata = await erc165
                .SupportsInterfaceQueryAsync("0x5b5e139f").ConfigureAwait(false);
            Assert.True(supportsErc721Metadata);

            var supportsErc1155 = await erc165
                .SupportsInterfaceQueryAsync("0xd9b67a26").ConfigureAwait(false);
            Assert.False(supportsErc1155);
        }
    }
}
