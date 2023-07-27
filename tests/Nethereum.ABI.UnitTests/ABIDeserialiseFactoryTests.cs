using System.Linq;
using Nethereum.ABI.ABIDeserialisation;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class CompilationOuputTests
    {
        string jsonOuput = @"{

  ""compiler"": {

    ""keccak256"": ""0x123..."",

    ""version"": ""0.8.2+commit.661d1103""
  },

  ""language"": ""Solidity"",

  ""output"": {

    ""abi"": [],

    ""devdoc"": {

      ""author"": ""John Doe"",

      ""details"": ""Interface of the ERC20 standard as defined in the EIP. See https://eips.ethereum.org/EIPS/eip-20 for details"",
      ""errors"": {
        ""MintToZeroAddress()"" : {
          ""details"": ""Cannot mint to zero address""
        }
      },
      ""events"": {
        ""Transfer(address,address,uint256)"": {
          ""details"": ""Emitted when `value` tokens are moved from one account (`from`) toanother (`to`)."",
          ""params"": {
            ""from"": ""The sender address"",
            ""to"": ""The receiver address"",
            ""value"": ""The token amount""
          }
        }
      },
      ""kind"": ""dev"",
      ""methods"": {
        ""transfer(address,uint256)"": {

          ""details"": ""Returns a boolean value indicating whether the operation succeeded. Must be called by the token holder address"",

          ""params"": {
            ""_value"": ""The amount tokens to be transferred"",
            ""_to"": ""The receiver address""
          },

          ""returns"": {

            ""success"": ""a boolean value indicating whether the operation succeeded""
          }
        }
      },
      ""stateVariables"": {
        ""owner"": {

          ""details"": ""Must be set during contract creation. Can then only be changed by the owner""
        }
      },

      ""title"": ""MyERC20: an example ERC20"",
      ""version"": 1 
    },

    ""userdoc"": {
      ""errors"": {
        ""ApprovalCallerNotOwnerNorApproved()"": 
          {
            ""notice"": ""The caller must own the token or be an approved operator.""
          }
        
      },
      ""events"": {
        ""Transfer(address,address,uint256)"": {
          ""notice"": ""`_value` tokens have been moved from `from` to `to`""
        }
      },
      ""kind"": ""user"",
      ""methods"": {
        ""transfer(address,uint256)"": {
          ""notice"": ""Transfers `_value` tokens to address `_to`""
        }
      },
      ""version"": 1 
    }
  },

  ""settings"": {

    ""compilationTarget"": {
      ""myDirectory/myFile.sol"": ""MyContract""
    },

    ""evmVersion"": ""london"",

    ""libraries"": {
      ""MyLib"": ""0x123123...""
    },
    ""metadata"": {

      ""appendCBOR"": true,

      ""bytecodeHash"": ""ipfs"",

      ""useLiteralContent"": true
    },

    ""optimizer"": {
      ""details"": {
        ""constantOptimizer"": false,
        ""cse"": false,
        ""deduplicate"": false,

        ""inliner"": false,

        ""jumpdestRemover"": true,
        ""orderLiterals"": false,

        ""peephole"": true,
        ""yul"": true,

        ""yulDetails"": {
          ""optimizerSteps"": ""dhfoDgvulfnTUtnIf..."",
          ""stackAllocation"": false
        }
      },
      ""enabled"": true,
      ""runs"": 500
    },

    ""remappings"": [ "":g=/dir"" ]
  },

  ""sources"": {
    ""destructible"": {

      ""content"": ""contract destructible is owned { function destroy() { if (msg.sender == owner) selfdestruct(owner); } }"",

      ""keccak256"": ""0x234...""
    },
    ""myDirectory/myFile.sol"": {

      ""keccak256"": ""0x123..."",

      ""license"": ""MIT"",

      ""urls"": [ ""bzz-raw://7d7a..."", ""dweb:/ipfs/QmN..."" ]
    }
  },

  ""version"": 1
}";


        [Fact]
        public void ShouldDeserialiseJsonCompilationOutput() 
        {
            var result =  CompilationMetadata.CompilationMetadataDeserialiser.DeserialiseCompilationMetadata(jsonOuput);
        }
    }


    public class ABIDeserialiseFactoryTests
    {
        [Fact]
        public void ShouldDeserialiseExtractStringSignatures()
        {
          
            string signature = 
@"event FeePercentUpdated(uint256 newFeePercent))
  constructor(address erc20)";
            var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(signature);
            var eventAbi = contractAbi.Events[0];
            Assert.Equal("FeePercentUpdated", eventAbi.Name);
            Assert.Equal("newFeePercent", eventAbi.InputParameters[0].Name);
            Assert.Equal("uint256", eventAbi.InputParameters[0].Type);
            Assert.False(eventAbi.InputParameters[0].Indexed);

            var constructor = contractAbi.Constructor;
            Assert.Equal("erc20", constructor.InputParameters[0].Name);
            Assert.Equal("address", constructor.InputParameters[0].Type);
        }

        [Fact]
        public void ShouldDeserialiseJson()
        {
            var abi =
                "[{\"constant\":false,\"inputs\":[],\"name\":\"txRaiseEvent\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"x\",\"type\":\"bytes32\"},{\"name\":\"truncateToLength\",\"type\":\"uint256\"}],\"name\":\"bytes32ToString\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"pure\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"source\",\"type\":\"string\"}],\"name\":\"stringToBytes32\",\"outputs\":[{\"name\":\"result\",\"type\":\"bytes32\"}],\"payable\":false,\"stateMutability\":\"pure\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"buyerSysId\",\"type\":\"bytes32\"},{\"indexed\":true,\"name\":\"buyerPurchaseOrderNumber\",\"type\":\"bytes32\"},{\"indexed\":true,\"name\":\"buyerProductId\",\"type\":\"bytes32\"},{\"components\":[{\"name\":\"ethPurchaseOrderNumber\",\"type\":\"uint64\"},{\"name\":\"buyerSysId\",\"type\":\"bytes32\"},{\"name\":\"buyerPurchaseOrderNumber\",\"type\":\"bytes32\"},{\"name\":\"sellerSysId\",\"type\":\"bytes32\"},{\"name\":\"sellerSalesOrderNumber\",\"type\":\"bytes32\"},{\"name\":\"buyerProductId\",\"type\":\"bytes32\"},{\"name\":\"sellerProductId\",\"type\":\"bytes32\"},{\"name\":\"currency\",\"type\":\"bytes32\"},{\"name\":\"currencyAddress\",\"type\":\"address\"},{\"name\":\"totalQuantity\",\"type\":\"uint32\"},{\"name\":\"totalValue\",\"type\":\"uint32\"},{\"name\":\"openInvoiceQuantity\",\"type\":\"uint32\"},{\"name\":\"openInvoiceValue\",\"type\":\"uint32\"},{\"name\":\"poStatus\",\"type\":\"uint8\"}],\"indexed\":false,\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"PurchaseRaisedLog\",\"type\":\"event\"}]";
            var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(abi);
            var eventAbi = contractAbi.Events.FirstOrDefault(e => e.Name == "PurchaseRaisedLog");
            Assert.Equal("8004ab55fa321fe77c5bde5420bc7af7d33f755d3d71ee4d59bf0c0af7b9c055", eventAbi.Sha3Signature);
        }
    }
}