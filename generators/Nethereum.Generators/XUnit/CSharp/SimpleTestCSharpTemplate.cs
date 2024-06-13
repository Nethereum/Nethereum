using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.XUnit
{
    public class SimpleTestCSharpTemplate : ClassTemplateBase<SimpleTestModel>
    {
        public SimpleTestCSharpTemplate(SimpleTestModel model):base(model)
        {
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
                $@"
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}: NethereumIntegrationTest 
{SpaceUtils.One__Tab}{{
{SpaceUtils.One__Tab}
{SpaceUtils.Two___Tabs}// ITestOutputHelper outputs information using XUnit
{SpaceUtils.Two___Tabs}// The default account (private key) for Nethereum testing is used as the parameter ""DefaultTestAccountConstants.PrivateKey"", 
{SpaceUtils.Two___Tabs}// you can use any of the preconfigured TestChains with that account for testing https://github.com/Nethereum/TestChains
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}(ITestOutputHelper xunitTestOutputHelper) : base(""http://localhost:8545"",
{SpaceUtils.Three____Tabs}DefaultTestAccountConstants.PrivateKey, new NethereumTestDebugLogger(new XunitOutputWriter(xunitTestOutputHelper)))
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}/// *** UNIT TEST SAMPLE USING ERC20 Standard Token *** //
{SpaceUtils.Two___Tabs}[Theory]
{SpaceUtils.Two___Tabs}[InlineData(10000)]
{SpaceUtils.Two___Tabs}[InlineData(5000)]
{SpaceUtils.Two___Tabs}[InlineData(300)]
{SpaceUtils.Two___Tabs}public async Task AfterDeployment_BalanceOwner_ShouldBeTheSameAsInitialSupply(int initialSupply)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}// Constructing the default deployment message with token name, decimal units
{SpaceUtils.Three____Tabs}var contractDeploymentDefault = GetDeploymentMessage();
{SpaceUtils.Three____Tabs}// Setting the supply to the theory
{SpaceUtils.Three____Tabs}contractDeploymentDefault.InitialAmount = initialSupply;
{SpaceUtils.Three____Tabs}// Given that we deploy the smart contract 
{SpaceUtils.Three____Tabs}GivenADeployedContract(contractDeploymentDefault);
{SpaceUtils.Three____Tabs}// Set up the expectation to be the balance of the owner the same as the initial supply
{SpaceUtils.Three____Tabs}var balanceOfExpectedResult = new BalanceOfOutputDTO() {{ Balance = initialSupply }};
{SpaceUtils.Three____Tabs}// when querying the smart contract to get the the balance of the owner we will expect the result to be same as the intial supply
{SpaceUtils.Three____Tabs}WhenQueryingThen(SimpleStandardContractTest.GetBalanceOfOwnerMessage(), balanceOfExpectedResult);
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}[Theory]
{SpaceUtils.Two___Tabs}[InlineData(10000)]
{SpaceUtils.Two___Tabs}[InlineData(5000)]
{SpaceUtils.Two___Tabs}[InlineData(300)]
{SpaceUtils.Two___Tabs}public async Task Transfering_ShouldIncreaseTheBalanceOfReceiver(int valueToSend)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var contractDeploymentDefault = SimpleStandardContractTest.GetDeploymentMessage();
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}Assert.False(valueToSend > contractDeploymentDefault.InitialAmount, ""value to send is bigger than the total supply, please adjust the test data"");
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}GivenADeployedContract(contractDeploymentDefault);
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}var receiver = SimpleStandardContractTest.ReceiverAddress;
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}var transferMessage = new TransferFunction()
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Three____Tabs}Value = valueToSend,
{SpaceUtils.Three____Tabs}FromAddress = DefaultTestAccountConstants.Address,
{SpaceUtils.Three____Tabs}To = receiver,
{SpaceUtils.Three____Tabs}}};
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}var expectedEvent = new TransferEventDTO()
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Four_____Tabs}From = DefaultTestAccountConstants.Address.ToLower(), 
{SpaceUtils.Four_____Tabs}To = SimpleStandardContractTest.ReceiverAddress.ToLower(),
{SpaceUtils.Four_____Tabs}Value = valueToSend
{SpaceUtils.Three____Tabs}}};
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}GivenATransaction(transferMessage).
{SpaceUtils.Four_____Tabs}ThenExpectAnEvent(expectedEvent);
{SpaceUtils.Four_____Tabs}
{SpaceUtils.Three____Tabs}var queryBalanceReceiverMessage = new BalanceOfFunction() {{ Owner = ReceiverAddress }};
{SpaceUtils.Three____Tabs}var balanceOfExpectedResult = new BalanceOfOutputDTO() {{ Balance = valueToSend }};
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}WhenQuerying<BalanceOfFunction, BalanceOfOutputDTO>(queryBalanceReceiverMessage)
{SpaceUtils.Four_____Tabs}.ThenExpectResult(balanceOfExpectedResult);
{SpaceUtils.Three____Tabs}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}public static string ReceiverAddress = ""0x31230d2cce102216644c59daE5baed380d84830c"";
{SpaceUtils.Five______Tabs}
{SpaceUtils.Two___Tabs}// Simple scenario for deployment
{SpaceUtils.Three____Tabs}public static StandardTokenDeployment GetDeploymentMessage()
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Four_____Tabs}return new StandardTokenDeployment()
{SpaceUtils.Four_____Tabs}{{
{SpaceUtils.Five______Tabs}InitialAmount = 10000000,
{SpaceUtils.Five______Tabs}TokenName = ""TST"",
{SpaceUtils.Five______Tabs}TokenSymbol = ""TST"",
{SpaceUtils.Five______Tabs}DecimalUnits = 18,
{SpaceUtils.Five______Tabs}FromAddress = DefaultTestAccountConstants.Address
{SpaceUtils.Four_____Tabs}}};
{SpaceUtils.Three____Tabs}}}
{SpaceUtils.Five______Tabs}
{SpaceUtils.Three____Tabs}public static BalanceOfFunction GetBalanceOfOwnerMessage()
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Four_____Tabs}return new BalanceOfFunction()
{SpaceUtils.Four_____Tabs}{{
{SpaceUtils.Five______Tabs}Owner = DefaultTestAccountConstants.Address
{SpaceUtils.Four_____Tabs}}};
{SpaceUtils.Three____Tabs}}}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Two___Tabs}//*** Etherum / Nethereum CQS Messages. These classes will normally be included in your Ethereum contract integration project *** ///
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}// Standard token deployment
{SpaceUtils.Two___Tabs}public class StandardTokenDeployment : ContractDeploymentMessage
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}public static string BYTECODE = ""6060604052341561000f57600080fd5b6040516107ae3803806107ae833981016040528080519190602001805182019190602001805191906020018051600160a060020a03331660009081526001602052604081208790558690559091019050600383805161007292916020019061009f565b506004805460ff191660ff8416179055600581805161009592916020019061009f565b505050505061013a565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100e057805160ff191683800117855561010d565b8280016001018555821561010d579182015b8281111561010d5782518255916020019190600101906100f2565b5061011992915061011d565b5090565b61013791905b808211156101195760008155600101610123565b90565b610665806101496000396000f3006060604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017357806323b872dd1461019857806327e235e3146101c0578063313ce567146101df5780635c6581651461020857806370a082311461022d57806395d89b411461024c578063a9059cbb1461025f578063dd62ed3e14610281575b600080fd5b34156100be57600080fd5b6100c66102a6565b60405160208082528190810183818151815260200191508051906020019080838360005b838110156101025780820151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b341561014857600080fd5b61015f600160a060020a0360043516602435610344565b604051901515815260200160405180910390f35b341561017e57600080fd5b6101866103b0565b60405190815260200160405180910390f35b34156101a357600080fd5b61015f600160a060020a03600435811690602435166044356103b6565b34156101cb57600080fd5b610186600160a060020a03600435166104bc565b34156101ea57600080fd5b6101f26104ce565b60405160ff909116815260200160405180910390f35b341561021357600080fd5b610186600160a060020a03600435811690602435166104d7565b341561023857600080fd5b610186600160a060020a03600435166104f4565b341561025757600080fd5b6100c661050f565b341561026a57600080fd5b61015f600160a060020a036004351660243561057a565b341561028c57600080fd5b610186600160a060020a036004358116906024351661060e565b60038054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b820191906000526020600020905b81548152906001019060200180831161031f57829003601f168201915b505050505081565b600160a060020a03338116600081815260026020908152604080832094871680845294909152808220859055909291907f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259085905190815260200160405180910390a350600192915050565b60005481565b600160a060020a0380841660008181526002602090815260408083203390951683529381528382205492825260019052918220548390108015906103fa5750828110155b151561040557600080fd5b600160a060020a038085166000908152600160205260408082208054870190559187168152208054849003905560001981101561046a57600160a060020a03808616600090815260026020908152604080832033909416835292905220805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8560405190815260200160405180910390a3506001949350505050565b60016020526000908152604090205481565b60045460ff1681565b600260209081526000928352604080842090915290825290205481565b600160a060020a031660009081526001602052604090205490565b60058054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b600160a060020a033316600090815260016020526040812054829010156105a057600080fd5b600160a060020a033381166000818152600160205260408082208054879003905592861680825290839020805486019055917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9085905190815260200160405180910390a350600192915050565b600160a060020a039182166000908152600260209081526040808320939094168252919091522054905600a165627a7a723058201145b253e40a502d8bd264f98d66de641dec0c9e4a25e35eaba523821e0fb6ad0029"";
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}public StandardTokenDeployment() : base(BYTECODE)
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Three____Tabs}}}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}public StandardTokenDeployment(string byteCode) : base(byteCode)
{SpaceUtils.Three____Tabs}{{
{SpaceUtils.Three____Tabs}}}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}[Parameter(""uint256"", ""_initialAmount"", 1)]
{SpaceUtils.Three____Tabs}public BigInteger InitialAmount {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""string"", ""_tokenName"", 2)]
{SpaceUtils.Three____Tabs}public string TokenName {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""uint8"", ""_decimalUnits"", 3)]
{SpaceUtils.Three____Tabs}public byte DecimalUnits {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""string"", ""_tokenSymbol"", 4)]
{SpaceUtils.Three____Tabs}public string TokenSymbol {{ get; set; }}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Five______Tabs}
{SpaceUtils.Two___Tabs}// Standard token transfer
{SpaceUtils.Two___Tabs}[Function(""transfer"", ""bool"")]
{SpaceUtils.Two___Tabs}public class TransferFunction : ContractMessage
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}[Parameter(""address"", ""_to"", 1)]
{SpaceUtils.Three____Tabs}public string To {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""uint256"", ""_value"", 2)]
{SpaceUtils.Three____Tabs}public BigInteger Value {{ get; set; }}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}//Standard token balanceOf
{SpaceUtils.Two___Tabs}[Function(""balanceOf"", typeof(BalanceOfOutputDTO))]
{SpaceUtils.Two___Tabs}public class BalanceOfFunction : ContractMessage
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}[Parameter(""address"", ""_owner"", 1)]
{SpaceUtils.Three____Tabs}public string Owner {{ get; set; }}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}//Standard token balanceOf Return
{SpaceUtils.Two___Tabs}[FunctionOutput]
{SpaceUtils.Two___Tabs}public class BalanceOfOutputDTO
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}[Parameter(""uint256"", ""balance"", 1)]
{SpaceUtils.Three____Tabs}public BigInteger Balance {{ get; set; }}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}//Standard token Transfer Event 
{SpaceUtils.Two___Tabs}[Event(""Transfer"")]
{SpaceUtils.Two___Tabs}public class TransferEventDTO
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}[Parameter(""address"", ""_from"", 1, true)]
{SpaceUtils.Three____Tabs}public string From {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""address"", ""_to"", 2, true)]
{SpaceUtils.Three____Tabs}public string To {{ get; set; }}
{SpaceUtils.Three____Tabs}[Parameter(""uint256"", ""_value"", 3, false)]
{SpaceUtils.Three____Tabs}public BigInteger Value {{ get; set; }}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.Two___Tabs}
{SpaceUtils.One__Tab}}}";
           
        }
    }
}