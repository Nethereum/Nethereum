using System;
using System.Numerics;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class TransactionProcessorTests
    {
        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        [Fact]
        public void CalculateIntrinsicGas_EmptyData_MessageCall()
        {
            var gas = TransactionProcessor.CalculateIntrinsicGas(null, isContractCreation: false);

            Assert.Equal(21000, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_EmptyData_ContractCreation()
        {
            var gas = TransactionProcessor.CalculateIntrinsicGas(null, isContractCreation: true);

            Assert.Equal(21000 + 32000, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_WithZeroBytes()
        {
            var data = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            var gas = TransactionProcessor.CalculateIntrinsicGas(data, isContractCreation: false);

            Assert.Equal(21000 + 4 * 4, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_WithNonZeroBytes()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var gas = TransactionProcessor.CalculateIntrinsicGas(data, isContractCreation: false);

            Assert.Equal(21000 + 4 * 16, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_MixedBytes()
        {
            var data = new byte[] { 0x00, 0x01, 0x00, 0x02 };
            var gas = TransactionProcessor.CalculateIntrinsicGas(data, isContractCreation: false);

            Assert.Equal(21000 + 2 * 4 + 2 * 16, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_ContractCreationWithData()
        {
            var data = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };
            var gas = TransactionProcessor.CalculateIntrinsicGas(data, isContractCreation: true);

            var expectedGas = 21000 + 32000;
            int initcodeWords = (data.Length + 31) / 32;
            expectedGas += initcodeWords * 2;
            foreach (var b in data)
            {
                expectedGas += b == 0 ? 4 : 16;
            }

            Assert.Equal(expectedGas, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_EmptyByteArray_MessageCall()
        {
            var gas = TransactionProcessor.CalculateIntrinsicGas(Array.Empty<byte>(), isContractCreation: false);
            Assert.Equal(21000, gas);
        }

        [Fact]
        public void CalculateIntrinsicGas_LargeData_CalculatesCorrectly()
        {
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            var gas = TransactionProcessor.CalculateIntrinsicGas(data, isContractCreation: false);

            long expected = 21000;
            foreach (var b in data)
                expected += b == 0 ? 4 : 16;

            Assert.Equal(expected, gas);
        }

        [Fact]
        public void GetTransactionData_LegacyTransactionChainId_ExtractsFields()
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId,
                RecipientAddress, 1000, 5, 2_000_000_000, 21_000, "");
            var tx = TransactionFactory.CreateTransaction(signedTxHex);

            var data = TransactionProcessor.GetTransactionData(tx);

            Assert.Equal(5, data.Nonce);
            Assert.Equal(21_000, data.GasLimit);
            Assert.Equal(2_000_000_000, data.GasPrice);
            Assert.Equal(1000, data.Value);
            Assert.Contains("3c44cdddb6a900fa2b585dd299e03d12fa4293bc", data.To?.ToLowerInvariant() ?? "");
        }

        [Fact]
        public void GetTransactionData_LegacyTransaction_ExtractsFields()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x03 },
                gasPrice: new byte[] { 0x77, 0x35, 0x94, 0x00 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x0a },
                data: Array.Empty<byte>());

            var data = TransactionProcessor.GetTransactionData(tx);

            Assert.Equal(3, data.Nonce);
            Assert.Equal(21_000, data.GasLimit);
            Assert.Equal(10, data.Value);
        }

        [Fact]
        public void GetEffectiveGasPrice_LegacyTransaction_ReturnsGasPrice()
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId,
                RecipientAddress, 0, 0, 5_000_000_000, 21_000, "");
            var tx = TransactionFactory.CreateTransaction(signedTxHex);
            var data = TransactionProcessor.GetTransactionData(tx);

            var effectivePrice = data.GetEffectiveGasPrice(1_000_000_000);

            Assert.Equal(5_000_000_000, effectivePrice);
        }

        [Fact]
        public void GetEffectiveGasPrice_EIP1559_CalculatesCorrectly()
        {
            var data = new TransactionData
            {
                MaxFeePerGas = 10_000_000_000,
                MaxPriorityFeePerGas = 2_000_000_000,
                GasPrice = 0
            };

            var effectivePrice = data.GetEffectiveGasPrice(1_000_000_000);

            Assert.Equal(3_000_000_000, effectivePrice);
        }

        [Fact]
        public void GetEffectiveGasPrice_EIP1559_CapsAtMaxFee()
        {
            var data = new TransactionData
            {
                MaxFeePerGas = 3_000_000_000,
                MaxPriorityFeePerGas = 5_000_000_000,
                GasPrice = 0
            };

            var effectivePrice = data.GetEffectiveGasPrice(2_000_000_000);

            Assert.Equal(3_000_000_000, effectivePrice);
        }
    }
}
