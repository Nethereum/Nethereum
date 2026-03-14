using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class DecodeTransactionDocExampleTests
    {
        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger TokenAmount { get; set; }
        }

        [Function("approve", "bool")]
        public class ApproveFunction : FunctionMessage
        {
            [Parameter("address", "_spender", 1)]
            public string Spender { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger Value { get; set; }
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "decode-transactions", "Check if transaction matches Transfer function and decode parameters")]
        public void ShouldDecodeTransferFromTransactionInput()
        {
            var toAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var amount = BigInteger.Parse("1000000000000000000");

            var transfer = new TransferFunction
            {
                To = toAddress,
                TokenAmount = amount
            };

            var functionABI = ABITypedRegistry.GetFunctionABI<TransferFunction>();
            var encoder = new FunctionCallEncoder();
            var encodedData = encoder.EncodeRequest(transfer, functionABI.Sha3Signature);

            var txn = new Transaction { Input = encodedData };

            Assert.True(txn.IsTransactionForFunctionMessage<TransferFunction>());

            var decoded = new TransferFunction().DecodeTransaction(txn);
            Assert.Equal(toAddress, decoded.To.ToLowerInvariant());
            Assert.Equal(amount, decoded.TokenAmount);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "decode-transactions", "Non-matching function signature returns false")]
        public void ShouldReturnFalseForNonMatchingFunction()
        {
            var approve = new ApproveFunction
            {
                Spender = "0x13f022d72158410433cbd66f5dd8bf6d2d129924",
                Value = BigInteger.Parse("1000000000000000000")
            };

            var functionABI = ABITypedRegistry.GetFunctionABI<ApproveFunction>();
            var encoder = new FunctionCallEncoder();
            var encodedData = encoder.EncodeRequest(approve, functionABI.Sha3Signature);

            var txn = new Transaction { Input = encodedData };

            Assert.False(txn.IsTransactionForFunctionMessage<TransferFunction>());
            Assert.True(txn.IsTransactionForFunctionMessage<ApproveFunction>());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "decode-transactions", "Transfer function selector is a9059cbb")]
        public void TransferFunctionSelector_ShouldBeCorrect()
        {
            var functionABI = ABITypedRegistry.GetFunctionABI<TransferFunction>();
            Assert.Equal("a9059cbb", functionABI.Sha3Signature);
        }
    }
}
