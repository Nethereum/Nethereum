using System.Linq;
using Nethereum.ABI.ABIDeserialisation;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
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