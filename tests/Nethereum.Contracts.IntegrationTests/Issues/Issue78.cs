using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.Issues
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class Issue78
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public Issue78(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task ProdContract()
        {
            var byteCode =
                "0x606060405260405161069d38038061069d833981016040528080518201919060200150505b33600160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908302179055508060036000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061009e57805160ff19168380011785556100cf565b828001600101855582156100cf579182015b828111156100ce5782518260005055916020019190600101906100b0565b5b5090506100fa91906100dc565b808211156100f657600081815060009055506001016100dc565b5090565b50505b506105918061010c6000396000f360606040526000357c0100000000000000000000000000000000000000000000000000000000900480632d202d24146100685780634ba200501461009b578063893d20e81461011b578063a0e67e2b14610159578063d5d1e770146101b557610063565b610002565b346100025761008360048080359060200190919050506101df565b60405180821515815260200191505060405180910390f35b34610002576100ad600480505061027a565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600302600f01f150905090810190601f16801561010d5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b346100025761012d6004805050610336565b604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390f35b346100025761016b6004805050610365565b60405180806020018281038252838181518152602001915080519060200190602002808383829060006004602084601f0104600302600f01f1509050019250505060405180910390f35b34610002576101c760048050506103f7565b60405180821515815260200191505060405180910390f35b60003373ffffffffffffffffffffffffffffffffffffffff16600160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff161415156102415760009050610275565b81600260006101000a81548173ffffffffffffffffffffffffffffffffffffffff0219169083021790555060019050610275565b919050565b602060405190810160405280600081526020015060036000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156103275780601f106102fc57610100808354040283529160200191610327565b820191906000526020600020905b81548152906001019060200180831161030a57829003601f168201915b50505050509050610333565b90565b6000600160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff169050610362565b90565b602060405190810160405280600081526020015060006000508054806020026020016040519081016040528092919081815260200182805480156103e857602002820191906000526020600020905b8160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16815260200190600101908083116103b4575b505050505090506103f4565b90565b60003373ffffffffffffffffffffffffffffffffffffffff16600260009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16141515610459576000905061058e565b600060005080548060010182818154818355818115116104ab578183600052602060002091820191016104aa919061048c565b808211156104a6576000818150600090555060010161048c565b5090565b5b5050509190906000526020600020900160005b600260009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16909190916101000a81548173ffffffffffffffffffffffffffffffffffffffff0219169083021790555050600260009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16600160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908302179055506000600260006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908302179055506001905061058e565b9056";
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""nextOwner"",""type"":""address""}],""name"":""setNextOwner"",""outputs"":[{""name"":""set"",""type"":""bool""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getProduct"",""outputs"":[{""name"":""product"",""type"":""string""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getOwner"",""outputs"":[{""name"":""owner"",""type"":""address""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getOwners"",""outputs"":[{""name"":""owners"",""type"":""address[]""}],""payable"":false,""type"":""function""},{""constant"":false,""inputs"":[],""name"":""confirmOwnership"",""outputs"":[{""name"":""confirmed"",""type"":""bool""}],""payable"":false,""type"":""function""},{""inputs"":[{""name"":""productDigest"",""type"":""string""}],""type"":""constructor""}]";

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var account = EthereumClientIntegrationFixture.AccountAddress;

            var contractHash = await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, account,
                new HexBigInteger(900 * 1000), "My product").ConfigureAwait(false);

            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(contractHash).ConfigureAwait(false);
            while (receipt == null)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(contractHash).ConfigureAwait(false);
            }

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);
            var code = await web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress).ConfigureAwait(false);

            Assert.True(!string.IsNullOrEmpty(code) && code.Length > 3);

            var function = contract.GetFunction("getProduct");
            var result = await function.CallAsync<string>().ConfigureAwait(false);

            Assert.Equal("My product", result);
        }
    }
}