using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts.Standards
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ERC1271Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ERC1271Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class ERC1271MockDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "608080604052346026575f80546001600160a01b031916331790556101a0908161002b8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c80631626ba7e1461003457638da5cb5b1461002f575f80fd5b610102565b346100ea5760403660031901126100ea5760243567ffffffffffffffff81116100ea57366023820112156100ea5780600401359067ffffffffffffffff82116100ee57604051601f8301601f19908116603f0116810167ffffffffffffffff8111828210176100ee5760405282815236602484840101116100ea575f6020846100e69560246100cb96018386013783010152610129565b6040516001600160e01b031990911681529081906020820190565b0390f35b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b346100ea575f3660031901126100ea575f546040516001600160a01b039091168152602090f35b601481511015610141575b506001600160e01b031990565b601401515f546001600160a01b0391821691161461015f575f610134565b630b135d3f60e11b9056fea26469706673582212202066723f3d93b94d5a83e4c8c2e3795fc8ce60785e6fb8a8a1678a4025af0cda64736f6c634300081c0033";

            public ERC1271MockDeployment() : base(BYTECODE) { }
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ERC-1271 signature validation for contract wallets", SkillName = "built-in-standards", Order = 7)]
        public async Task ShouldValidateContractSignature()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<ERC1271MockDeployment>()
                .SendRequestAndWaitForReceiptAsync(new ERC1271MockDeployment()).ConfigureAwait(false);

            var contractAddress = deploymentReceipt.ContractAddress;
            var erc1271 = web3.Eth.ERC1271.GetContractService(contractAddress);

            var messageHash = new byte[32];
            messageHash[0] = 0x01;

            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;
            var ownerBytes = ownerAddress.HexToByteArray();
            var validSignature = new byte[20];
            System.Array.Copy(ownerBytes, validSignature, 20);

            var isValid = await erc1271
                .IsValidSignatureAndValidateReturnQueryAsync(messageHash, validSignature)
                .ConfigureAwait(false);
            Assert.True(isValid);

            var invalidSignature = new byte[20];
            var isInvalid = await erc1271
                .IsValidSignatureAndValidateReturnQueryAsync(messageHash, invalidSignature)
                .ConfigureAwait(false);
            Assert.False(isInvalid);
        }
    }
}
