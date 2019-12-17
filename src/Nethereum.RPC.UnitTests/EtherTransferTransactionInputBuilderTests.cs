using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Xunit;
using Nethereum.RPC.TransactionManagers;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.UnitTests
{
    public class EtherTransferTransactionInputBuilderTests
    {
        private const string FromAddress = "0x12890D2cce102216644c59daE5baed380d84830c";
        private const string ToAddress = "0x407D73d8a49eeb85D32Cf465507dd71d507100c1";
        private const decimal EthAmount = 1;
        private const decimal GasPriceGwei = 2;
        private readonly BigInteger _gas = 3;
        private readonly BigInteger _nonce = 4;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateTransactionInput_IncorrectToAddress_ThrowsArgumentNullException(string toAddress)
        {
            string expectedMessage = "Value cannot be null";

            Exception ex = Assert.Throws<ArgumentNullException>(() =>
                EtherTransferTransactionInputBuilder.CreateTransactionInput(null, toAddress, 0));

            Assert.StartsWith(expectedMessage, ex.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void CreateTransactionInput_IncorrectEtherAmount_ThrowsArgumentOutOfRangeException(decimal etherAmount)
        {
            string expectedMessage = "Specified argument was out of the range of valid values";

            Exception ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                EtherTransferTransactionInputBuilder.CreateTransactionInput(null, ToAddress, etherAmount));

            Assert.StartsWith(expectedMessage, ex.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void CreateTransactionInput_IncorrectGasPriceGwei_ThrowsArgumentOutOfRangeException(decimal gasPriceGwei)
        {
            string expectedMessage = "Specified argument was out of the range of valid values.";

            Exception ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                EtherTransferTransactionInputBuilder.CreateTransactionInput(null, ToAddress, EthAmount, gasPriceGwei));

            Assert.StartsWith(expectedMessage, ex.Message);
        }

        [Fact]
        public void CreateTransactionInput_ReturnsTransactionInput()
        {
            var expectedGasPrice = new HexBigInteger(UnitConversion.Convert.ToWei(GasPriceGwei, UnitConversion.EthUnit.Gwei));
            var expectedValue = new HexBigInteger(UnitConversion.Convert.ToWei(EthAmount));
            var expectedGas = new HexBigInteger(_gas);
            var expectedNonce = new HexBigInteger(_nonce);

            var actualTransactionInput = EtherTransferTransactionInputBuilder.CreateTransactionInput(FromAddress, ToAddress, EthAmount, GasPriceGwei, _gas, _nonce);

            Assert.IsType<TransactionInput>(actualTransactionInput);
            Assert.Equal(ToAddress, actualTransactionInput.To);
            Assert.Equal(FromAddress, actualTransactionInput.From);
            Assert.Equal(expectedGasPrice, actualTransactionInput.GasPrice);
            Assert.Equal(expectedValue, actualTransactionInput.Value);
            Assert.Equal(expectedGas, actualTransactionInput.Gas);
            Assert.Equal(expectedNonce, actualTransactionInput.Nonce);
        }

        [Fact]
        public void CreateTransactionInput_ReturnsTransactionInput2()
        {
            var expectedGasPrice = new HexBigInteger(UnitConversion.Convert.ToWei(GasPriceGwei, UnitConversion.EthUnit.Gwei));
            var expectedValue = new HexBigInteger(UnitConversion.Convert.ToWei(EthAmount));

            var actualTransactionInput = EtherTransferTransactionInputBuilder.CreateTransactionInput(FromAddress, ToAddress, EthAmount, GasPriceGwei, null, null);

            Assert.IsType<TransactionInput>(actualTransactionInput);
            Assert.Equal(ToAddress, actualTransactionInput.To);
            Assert.Equal(FromAddress, actualTransactionInput.From);
            Assert.Equal(expectedGasPrice, actualTransactionInput.GasPrice);
            Assert.Equal(expectedValue, actualTransactionInput.Value);
            Assert.Null(actualTransactionInput.Gas);
            Assert.Null(actualTransactionInput.Nonce);
        }
    }
}