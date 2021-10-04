using System.Linq;
using Nethereum.ABI.JsonDeserialisation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class AbiDeserialiseTuplesTests
    {
        [Fact]
        public void ShouldDeserialisedAndEncodeSignatureEventsWithTuples()
        {
            var abi =
                "[{\"constant\":false,\"inputs\":[],\"name\":\"txRaiseEvent\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"x\",\"type\":\"bytes32\"},{\"name\":\"truncateToLength\",\"type\":\"uint256\"}],\"name\":\"bytes32ToString\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"pure\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"source\",\"type\":\"string\"}],\"name\":\"stringToBytes32\",\"outputs\":[{\"name\":\"result\",\"type\":\"bytes32\"}],\"payable\":false,\"stateMutability\":\"pure\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"buyerSysId\",\"type\":\"bytes32\"},{\"indexed\":true,\"name\":\"buyerPurchaseOrderNumber\",\"type\":\"bytes32\"},{\"indexed\":true,\"name\":\"buyerProductId\",\"type\":\"bytes32\"},{\"components\":[{\"name\":\"ethPurchaseOrderNumber\",\"type\":\"uint64\"},{\"name\":\"buyerSysId\",\"type\":\"bytes32\"},{\"name\":\"buyerPurchaseOrderNumber\",\"type\":\"bytes32\"},{\"name\":\"sellerSysId\",\"type\":\"bytes32\"},{\"name\":\"sellerSalesOrderNumber\",\"type\":\"bytes32\"},{\"name\":\"buyerProductId\",\"type\":\"bytes32\"},{\"name\":\"sellerProductId\",\"type\":\"bytes32\"},{\"name\":\"currency\",\"type\":\"bytes32\"},{\"name\":\"currencyAddress\",\"type\":\"address\"},{\"name\":\"totalQuantity\",\"type\":\"uint32\"},{\"name\":\"totalValue\",\"type\":\"uint32\"},{\"name\":\"openInvoiceQuantity\",\"type\":\"uint32\"},{\"name\":\"openInvoiceValue\",\"type\":\"uint32\"},{\"name\":\"poStatus\",\"type\":\"uint8\"}],\"indexed\":false,\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"PurchaseRaisedLog\",\"type\":\"event\"}]";
            var contractAbi = new ABIDeserialiser().DeserialiseContract(abi);
            var eventAbi = contractAbi.Events.FirstOrDefault(e => e.Name == "PurchaseRaisedLog");
            Assert.Equal("8004ab55fa321fe77c5bde5420bc7af7d33f755d3d71ee4d59bf0c0af7b9c055", eventAbi.Sha3Signature);
        }

        [Fact]
        public void ShouldDeserialiseAndEncodeSignatureFunctionsWithTuplesArrays()
        {
            var abi =
                "[{\"constant\":false,\"inputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"SetPurchaseOrder\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[],\"name\":\"GetPurchaseOrder2\",\"outputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"GetPurchaseOrder\",\"outputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"sender\",\"type\":\"address\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"indexed\":false,\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"PurchaseOrderChanged\",\"type\":\"event\"}]";
            var contractAbi = new ABIDeserialiser().DeserialiseContract(abi);
            var functionABI = contractAbi.Functions.FirstOrDefault(e => e.Name == "SetPurchaseOrder");
            var sig = functionABI.Sha3Signature;
            Assert.Equal("0cc400bd", functionABI.Sha3Signature);
        }

        [Fact]
        public void ShouldDeserialiseJArrayStyleABI()
        {
            var abi =
                "[{\"constant\":false,\"inputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"SetPurchaseOrder\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[],\"name\":\"GetPurchaseOrder2\",\"outputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"GetPurchaseOrder\",\"outputs\":[{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"sender\",\"type\":\"address\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"components\":[{\"name\":\"id\",\"type\":\"uint256\"},{\"name\":\"productId\",\"type\":\"uint256\"},{\"name\":\"quantity\",\"type\":\"uint256\"}],\"name\":\"lineItem\",\"type\":\"tuple[]\"},{\"name\":\"customerId\",\"type\":\"uint256\"}],\"indexed\":false,\"name\":\"purchaseOrder\",\"type\":\"tuple\"}],\"name\":\"PurchaseOrderChanged\",\"type\":\"event\"}]";
            var contractAbi = new ABIDeserialiser().DeserialiseContract(JArray.Parse(abi));
            var functionABI = contractAbi.Functions.FirstOrDefault(e => e.Name == "SetPurchaseOrder");
            var sig = functionABI.Sha3Signature;
            Assert.Equal("0cc400bd", functionABI.Sha3Signature);
        }

        [Fact]
        public void ShouldDeserialiseAndEncodeSignatureCustomErrorsWithTuplesArrays()
        {
            var abi =
                "[{\"inputs\":[],\"name\":\"ErrorWithNoParams\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"int256\",\"name\":\"x\",\"type\":\"int256\"},{\"internalType\":\"string\",\"name\":\"errorMessage\",\"type\":\"string\"}],\"name\":\"ErrorWithParams\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"CustomErrorNoParams\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"CustomErrorWithParams\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"NormalRevert\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";
            var contractAbi = new ABIDeserialiser().DeserialiseContract(abi);
            var errorABI = contractAbi.Errors.FirstOrDefault(e => e.Name == "ErrorWithParams");
            var sig = errorABI.Sha3Signature;
            Assert.Equal("650da04b", errorABI.Sha3Signature);
        }
    }
}