using System.Numerics;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class TransactionProcessorTests
    {
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
            foreach (var b in data)
            {
                expectedGas += b == 0 ? 4 : 16;
            }

            Assert.Equal(expectedGas, gas);
        }
    }
}
