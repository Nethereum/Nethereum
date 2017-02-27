using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Geth;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.Web3.Tests.Issues
{

    public class IssueGeth1_9
    {
        [Fact]
        public async Task ShouldBeAbleToDeploy()
        {

            var contractByteCode =
                "0x6060604052604060405190810160405280600a81526020017f4d756c7469706c6965720000000000000000000000000000000000000000000081526020015060016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061008c57805160ff19168380011785556100bd565b828001600101855582156100bd579182015b828111156100bc57825182600050559160200191906001019061009e565b5b5090506100e891906100ca565b808211156100e457600081815060009055506001016100ca565b5090565b5050604051602080610303833981016040528080519060200190919050505b806000600050819055505b506101e2806101216000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a901461004f57806375d0c0dc14610072578063c6888fa1146100ed5761004d565b005b61005c6004805050610119565b6040518082815260200191505060405180910390f35b61007f6004805050610141565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600f02600301f150905090810190601f1680156100df5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b610103600480803590602001909190505061012b565b6040518082815260200191505060405180910390f35b60006000600050549050610128565b90565b60006000600050548202905061013c565b919050565b60016000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156101da5780601f106101af576101008083540402835291602001916101da565b820191906000526020600020905b8154815290600101906020018083116101bd57829003601f168201915b50505050508156";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""contractName"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";


            var web3 = new Web3Geth(new ManagedAccount(addressFrom, pass));

            var transactionHash =
                await web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, addressFrom, new HexBigInteger(1999990), new HexBigInteger(10), new object[] {} );


        }
    }

    public class TestIssueGasAllDataOutput
    {
        [Fact]
        public async Task ShouldOutputAllData()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var password = "password";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""payable"":false,""type"":""function""},{""constant"":false,""inputs"":[{""name"":""customerName"",""type"":""string""},{""name"":""mobileNumber"",""type"":""int256""},{""name"":""serviceProvider"",""type"":""string""}],""name"":""addCustomer"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getAllCustomers"",""outputs"":[{""name"":""mobile"",""type"":""int256[]""},{""name"":""customerName"",""type"":""bytes32[]""},{""name"":""serviceProvider"",""type"":""bytes32[]""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[{""name"":""mobileNumber"",""type"":""int256""}],""name"":""get"",""outputs"":[{""name"":""mobile"",""type"":""int256""},{""name"":""customerName"",""type"":""bytes32""},{""name"":""serviceProvider"",""type"":""bytes32""}],""payable"":false,""type"":""function""},{""constant"":false,""inputs"":[{""name"":""mobileNumber"",""type"":""int256""}],""name"":""getCustomersByMobileNumber"",""outputs"":[{""name"":""mobile"",""type"":""uint256""},{""name"":""customerName"",""type"":""bytes32""},{""name"":""serviceProvider"",""type"":""bytes32""}],""payable"":false,""type"":""function""},{""inputs"":[],""type"":""constructor""}]";
            var byteCode = "606060405260006001556104f1806100176000396000f3606060405260e060020a60003504631df4f144811461004a5780634f5d64ce146100675780637da67ba814610121578063846719e0146102e15780638babdb681461034a575b610002565b346100025760076004350260408051918252519081900360200190f35b34610002576103746004808035906020019082018035906020019191908080601f0160208091040260200160405190810160405280939291908181526020018383808284375050604080516020604435808b0135601f81018390048302840183019094528383529799893599909860649850929650919091019350909150819084018382808284375094965050505050505060408051606081018252600060208201819052918101829052838152610448855b6020015190565b34610002576040805160208082018352600080835283518083018552818152845180840186528281528551808501875283815286518086018852848152875180870189528581528851606081018a5286815296870186905286890186905285549851610388999597949693949293919290869080591061019e5750595b9080825280602002602001820160405280156101b5575b509450856040518059106101c65750595b9080825280602002602001820160405280156101dd575b509350856040518059106101ee5750595b908082528060200260200182016040528015610205575b509250600091505b858260ff1610156104e3576002600050600060006000508460ff168154811015610002579060005260206000209001600050548152602080820192909252604090810160002081516060810183528154808252600183015494820194909452600290910154918101919091528651909250869060ff8516908110156100025760209081029091018101919091528101518451859060ff851690811015610002576020908102909101015260408101518351849060ff851690811015610002576020908102909101015260019091019061020d565b34610002576004357f61626300000000000000000000000000000000000000000000000000000000007f53330000000000000000000000000000000000000000000000000000000000005b60408051938452602084019290925282820152519081900360600190f35b3461000257600080546004358252600260208190526040909220600181015492015490919061032c565b604080519115158252519081900360200190f35b604051808060200180602001806020018481038452878181518152602001915080519060200190602002808383829060006004602084601f0104600302600f01f1509050018481038352868181518152602001915080519060200190602002808383829060006004602084601f0104600302600f01f1509050018481038252858181518152602001915080519060200190602002808383829060006004602084601f0104600302600f01f150905001965050505050505060405180910390f35b60208201526104568361011a565b604082015260008054600181018083558281838015829011610499576000838152602090206104999181019083015b808211156104df5760008155600101610485565b5050506000928352506020808320909101869055948152600280865260409182902083518155958301516001808801919091559290910151940193909355509092915050565b5090565b50929791965094509250505056";

            var web3 = new Web3Geth(new ManagedAccount(senderAddress, password));

            var transactionHash =
                await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(1999990));

            var mineResult = await web3.Miner.Start.SendRequestAsync(2);

            Assert.True(mineResult);

            var receipt = await MineAndGetReceiptAsync(web3, transactionHash);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var addCustomerFunction = contract.GetFunction("addCustomer");

            var getCustomersByMobileNumberFunction = contract.GetFunction("getCustomersByMobileNumber");

            var resultHash = await addCustomerFunction.SendTransactionAsync(senderAddress, new HexBigInteger(900000), new HexBigInteger(0), "Mahesh", 111, "Airtel");

            receipt = await MineAndGetReceiptAsync(web3, resultHash);

            var results = await getCustomersByMobileNumberFunction.CallDeserializingToObjectAsync<CustomerData>(111);

            Assert.Equal(results.customerName, "Mahesh");

            var getAllCustomers = contract.GetFunction("getAllCustomers");
            var results2 = await getAllCustomers.CallDeserializingToObjectAsync<AllCustomerData>();
        }

        public async Task<TransactionReceipt> MineAndGetReceiptAsync(Web3Geth web3, string transactionHash)
        {

            var miningResult = await web3.Miner.Start.SendRequestAsync(6);
            Assert.True(miningResult);

            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            miningResult = await web3.Miner.Stop.SendRequestAsync();
            Assert.True(miningResult);
            return receipt;
        }

        [FunctionOutput]
        public class CustomerData
        {
            [Parameter("int256", "mobile", 1)]
            public int mobile { get; set; }

            [Parameter("bytes32", "customerName", 2)]
            public string customerName { get; set; }

            [Parameter("bytes32", "serviceProvider", 3)]
            public string serviceProvider { get; set; }
        }

        [FunctionOutput]
        public class AllCustomerData
        {
            [Parameter("int256[]", "mobile", 1)]
            public List<BigInteger> mobile { get; set; }

            [Parameter("bytes32[]", "customerName", 2)]
            public List<string> customerName { get; set; }

            [Parameter("bytes32[]", "serviceProvider", 3)]
            public List<string> serviceProvider { get; set; }
        }
    }
}