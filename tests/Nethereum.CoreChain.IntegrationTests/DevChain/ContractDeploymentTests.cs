using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class ContractDeploymentTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;

        public ContractDeploymentTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DeployContract_ReturnsContractAddress()
        {
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success, $"Deployment failed: {result.RevertReason}");

            // Get contract address from ReceiptInfo (SendTransactionAsync doesn't populate ContractAddress)
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            Assert.NotNull(receiptInfo);
            Assert.NotNull(receiptInfo.ContractAddress);
            Assert.StartsWith("0x", receiptInfo.ContractAddress);
        }

        [Fact]
        public async Task DeployContract_StoresCodeAtAddress()
        {
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            // Get contract address from ReceiptInfo
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            Assert.NotNull(receiptInfo?.ContractAddress);

            var code = await _fixture.Node.GetCodeAsync(receiptInfo.ContractAddress);
            Assert.NotNull(code);
            Assert.NotEmpty(code);
        }

        [Fact]
        public async Task DeployContract_ReceiptHasContractAddress()
        {
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(txHash);
            Assert.NotNull(receiptInfo);
            Assert.NotNull(receiptInfo.ContractAddress);
            Assert.StartsWith("0x", receiptInfo.ContractAddress);
        }

        [Fact]
        public async Task DeployContract_ConsumesGas()
        {
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            // Get GasUsed from ReceiptInfo
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            Assert.True(receiptInfo.GasUsed > 21000, "Contract deployment should use more gas than simple transfer");
        }

        [Fact]
        public async Task DeployContract_GeneratesValidContractAddress()
        {
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            // Get contract address from ReceiptInfo
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            Assert.NotNull(receiptInfo?.ContractAddress);

            // Contract address should be a valid 20-byte hex address
            Assert.StartsWith("0x", receiptInfo.ContractAddress);
            Assert.Equal(42, receiptInfo.ContractAddress.Length); // 0x + 40 hex chars = 20 bytes

            // Contract should have code at the address
            var code = await _fixture.Node.GetCodeAsync(receiptInfo.ContractAddress);
            Assert.NotNull(code);
            Assert.NotEmpty(code);
        }

        [Fact]
        public async Task DeployContract_UpdatesAccountNonce()
        {
            var initialNonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode, nonce: initialNonce);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            var finalNonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            Assert.Equal(initialNonce + 1, finalNonce);
        }

        [Fact]
        public async Task DeployContract_DeductsGasCost()
        {
            var initialBalance = await _fixture.Node.GetBalanceAsync(_fixture.Address);
            BigInteger gasPrice = 1_000_000_000;

            var bytecode = SimpleStorageContract.GetDeploymentBytecode();
            var signedTx = _fixture.CreateContractDeploymentTransaction(bytecode);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);

            // Get GasUsed from ReceiptInfo
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            var expectedCost = gasPrice * receiptInfo.GasUsed;
            var finalBalance = await _fixture.Node.GetBalanceAsync(_fixture.Address);

            Assert.Equal(initialBalance - expectedCost, finalBalance);
        }
    }
}
