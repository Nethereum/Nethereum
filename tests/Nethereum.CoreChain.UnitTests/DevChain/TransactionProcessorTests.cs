using System;
using System.Collections.Generic;
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
        public void MinimumGasLimit_EmptyData_MessageCall_IsBaseCost()
        {
            var gas = EVM.HardforkConfig.Cancun.IntrinsicGasRules
                .CalculateMinimumGasLimit(null, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);

            Assert.Equal(21000, gas);
        }

        [Fact]
        public void MinimumGasLimit_Creation_IncludesInitcodeWordGas()
        {
            var data = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };
            var gas = EVM.HardforkConfig.Cancun.IntrinsicGasRules
                .CalculateMinimumGasLimit(data, isContractCreation: true, accessList: null, isSelfTransfer: false, hasValue: false);

            var expected = 21000 + 32000 + 2 * ((data.Length + 31) / 32);
            foreach (var b in data) expected += b == 0 ? 4 : 16;
            Assert.Equal(expected, gas);
        }

        [Fact]
        public void MinimumGasLimit_Prague_DataHeavyCall_FloorWins()
        {
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++) data[i] = 0x01;

            var gas = EVM.HardforkConfig.Prague.IntrinsicGasRules
                .CalculateMinimumGasLimit(data, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);

            Assert.Equal(61000, gas);
        }

        [Fact]
        public void MinimumGasLimit_Cancun_NoFloorRule_IntrinsicWins()
        {
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++) data[i] = 0x01;

            var gas = EVM.HardforkConfig.Cancun.IntrinsicGasRules
                .CalculateMinimumGasLimit(data, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);

            Assert.Equal(21000 + 1000 * 16, gas);
        }

        [Fact]
        public void Given_AnEip155LegacyTransaction_When_Read_Then_EveryScalarComesBackAsSigned()
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId,
                RecipientAddress, 1000, 5, 2_000_000_000, 21_000, "");
            var tx = TransactionFactory.CreateTransaction(signedTxHex);

            Assert.Equal(5, tx.GetNonce());
            Assert.Equal(21_000, tx.GetGasLimit());
            Assert.Equal(2_000_000_000, tx.GetMaxFeePerGas());
            Assert.Equal(1000, tx.GetValue());
            Assert.Contains("3c44cdddb6a900fa2b585dd299e03d12fa4293bc", tx.GetReceiverAddress()?.ToLowerInvariant() ?? "");
        }

        [Fact]
        public void Given_APlainLegacyTransaction_When_Read_Then_ItsRlpScalarsAreDecoded()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x03 },
                gasPrice: new byte[] { 0x77, 0x35, 0x94, 0x00 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x0a },
                data: Array.Empty<byte>());

            Assert.Equal(3, tx.GetNonce());
            Assert.Equal(21_000, tx.GetGasLimit());
            Assert.Equal(10, tx.GetValue());
        }

        [Fact]
        public void Given_ALegacyTransaction_When_Priced_Then_TheEffectiveGasPriceIsItsGasPrice()
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId,
                RecipientAddress, 0, 0, 5_000_000_000, 21_000, "");
            var tx = TransactionFactory.CreateTransaction(signedTxHex);

            Assert.Equal(5_000_000_000, tx.GetEffectiveGasPrice(1_000_000_000));
        }

        [Fact]
        public void Given_AnEip1559Transaction_When_Priced_Then_TheBaseFeeCarriesThePriorityFee()
        {
            var tx = FeeMarketTransaction(maxFeePerGas: 10_000_000_000, maxPriorityFeePerGas: 2_000_000_000);

            Assert.Equal(3_000_000_000, tx.GetEffectiveGasPrice(1_000_000_000));
        }

        [Fact]
        public void Given_AnEip1559TransactionWhosePriorityFeeExceedsItsMaxFee_When_Priced_Then_ItIsCappedAtTheMaxFee()
        {
            var tx = FeeMarketTransaction(maxFeePerGas: 3_000_000_000, maxPriorityFeePerGas: 5_000_000_000);

            Assert.Equal(3_000_000_000, tx.GetEffectiveGasPrice(2_000_000_000));
        }

        private static ISignedTransaction FeeMarketTransaction(
            BigInteger maxFeePerGas, BigInteger maxPriorityFeePerGas)
        {
            var signed = new TypeTransactionSigner<Transaction1559>().SignTransaction(
                PrivateKey,
                new Transaction1559(ChainId, 0, maxPriorityFeePerGas, maxFeePerGas, 21_000,
                    RecipientAddress, 0, "", new List<AccessListItem>()));
            return TransactionFactory.CreateTransaction(signed.HexToByteArray());
        }
    }
}
