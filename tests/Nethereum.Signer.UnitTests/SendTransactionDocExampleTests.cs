using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class SendTransactionDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "send-transaction", "Create TransactionInput with data encoded as hex UTF8")]
        public void ShouldCreateTransactionInputWithHexData()
        {
            var txnInput = new TransactionInput();
            txnInput.From = "0x12890D2cce102216644c59daE5baed380d84830c";
            txnInput.To = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            txnInput.Data = "Hello".ToHexUTF8();
            txnInput.Gas = new HexBigInteger(900000);

            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", txnInput.From);
            Assert.Equal("0x13f022d72158410433cbd66f5dd8bf6d2d129924", txnInput.To);
            Assert.Equal("0x48656c6c6f", txnInput.Data);
            Assert.Equal(new BigInteger(900000), txnInput.Gas.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "send-transaction", "TransactionReceipt HasErrors detects success and revert")]
        public void ShouldDetectTransactionErrors()
        {
            var successReceipt = new TransactionReceipt { Status = new HexBigInteger(1) };
            Assert.False(successReceipt.HasErrors().Value);

            var revertReceipt = new TransactionReceipt { Status = new HexBigInteger(0) };
            Assert.True(revertReceipt.HasErrors().Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "send-transaction", "EIP-1559 TransactionInput with MaxFeePerGas and MaxPriorityFeePerGas")]
        public void ShouldCreateEip1559TransactionInput()
        {
            var txnInput = new TransactionInput()
            {
                From = "0x12890D2cce102216644c59daE5baed380d84830c",
                To = "0x13f022d72158410433cbd66f5dd8bf6d2d129924",
                Data = "Hello".ToHexUTF8(),
                Gas = new HexBigInteger(900000),
                MaxFeePerGas = new HexBigInteger(Web3.Web3.Convert.ToWei(50, UnitConversion.EthUnit.Gwei)),
                MaxPriorityFeePerGas = new HexBigInteger(Web3.Web3.Convert.ToWei(2, UnitConversion.EthUnit.Gwei))
            };

            Assert.Equal(Web3.Web3.Convert.ToWei(50, UnitConversion.EthUnit.Gwei), txnInput.MaxFeePerGas.Value);
            Assert.Equal(Web3.Web3.Convert.ToWei(2, UnitConversion.EthUnit.Gwei), txnInput.MaxPriorityFeePerGas.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-replacement", "Increment gas price for transaction replacement")]
        public void ShouldIncrementGasPriceForReplacement()
        {
            var originalGasPrice = new BigInteger(20000000000);
            var increment = Web3.Web3.Convert.ToWei(1, UnitConversion.EthUnit.Gwei);
            var newGasPrice = originalGasPrice + increment;

            Assert.True(newGasPrice > originalGasPrice);
            Assert.Equal(originalGasPrice + 1000000000, newGasPrice);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-replacement", "Reuse nonce for transaction replacement")]
        public void ShouldReuseNonceForReplacement()
        {
            var originalNonce = new HexBigInteger(42);

            var txnInput1 = new TransactionInput { Nonce = originalNonce };
            var txnInput2 = new TransactionInput { Nonce = txnInput1.Nonce };

            Assert.Equal(txnInput1.Nonce.Value, txnInput2.Nonce.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "send-eth", "EtherTransferTransactionInputBuilder creates legacy transfer input")]
        public void ShouldBuildEtherTransferTransactionInput()
        {
            var from = "0x12890D2cce102216644c59daE5baed380d84830c";
            var to = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";

            var txnInput = EtherTransferTransactionInputBuilder
                .CreateTransactionInput(from, to, 1.11m, 2);

            Assert.Equal(from, txnInput.From);
            Assert.Equal(to, txnInput.To);
            Assert.Equal(Web3.Web3.Convert.ToWei(1.11m), txnInput.Value.Value);
            Assert.Equal(Web3.Web3.Convert.ToWei(2, UnitConversion.EthUnit.Gwei), txnInput.GasPrice.Value);
        }
    }
}
