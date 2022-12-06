using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Nethereum.ABI.FunctionEncoding;
using Newtonsoft.Json.Linq;


namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TestJsonAnonymousReturn
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TestJsonAnonymousReturn(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldReturnJsonWithEncodedDefaultNameWithOrderAndTypeIfNotIncluded()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<SimpleOwner2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            var contract = web3.Eth.GetContract("[{\"inputs\":[],\"name\":\"getOwner\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]", deploymentReceipt.ContractAddress);
            var functionGetOwner = contract.GetFunction("getOwner");
            var result = await functionGetOwner.CallDecodingToDefaultAsync(EthereumClientIntegrationFixture.AccountAddress, null, null, null, null);
            var jObjectResult = result.ConvertToJObject();
            var expectedJson = JObject.Parse("{\r\n  \"param_1_address\": \"0x12890D2cce102216644c59daE5baed380d84830c\",\r\n  \"param_2_uint256\": \"1\"\r\n}");
            Assert.True(JObject.DeepEquals(expectedJson, jObjectResult));
        }


        /*contract SimpleOwner2 {

            function getOwner() public view returns(address, uint) {
                return (msg.sender, 1);
            }
        }*/
        public class SimpleOwner2Deployment : ContractDeploymentMessage
        {
            public static string BYTECODE = "6080604052348015600f57600080fd5b50607d80601d6000396000f3fe6080604052348015600f57600080fd5b506004361060285760003560e01c8063893d20e814602d575b600080fd5b604080513381526001602082015281519081900390910190f3fea264697066735822122047ba7eb98d57a3784b68591a5cbfccdb8c58fe369ccb067bd70b18da89e52c0964736f6c63430008110033";
            public SimpleOwner2Deployment() : base(BYTECODE) { }
            public SimpleOwner2Deployment(string byteCode) : base(byteCode) { }

        }

        public partial class GetOwnerFunction : GetOwnerFunctionBase { }

        [Function("getOwner", typeof(GetOwnerOutputDTO))]
        public class GetOwnerFunctionBase : FunctionMessage
        {

        }

        public partial class GetOwnerOutputDTO : GetOwnerOutputDTOBase { }

        [FunctionOutput]
        public class GetOwnerOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("address", "", 1)]
            public virtual string ReturnValue1 { get; set; }
            [Parameter("uint256", "", 2)]
            public virtual BigInteger ReturnValue2 { get; set; }
        }
    }
}