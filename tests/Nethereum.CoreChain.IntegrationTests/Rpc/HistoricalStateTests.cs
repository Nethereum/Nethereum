using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class HistoricalStateTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly RpcDispatcher _dispatcher;

        public HistoricalStateTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(_fixture.Node, _fixture.ChainId, services);
            _dispatcher = new RpcDispatcher(registry, context);
        }

        [Fact]
        public async Task GetBalance_AtHistoricalBlock_ReturnsCorrectBalance()
        {
            var blockBefore = await _fixture.Node.GetBlockNumberAsync();

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("1000000000000000000")); // 1 ETH
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var blockAfter = await _fixture.Node.GetBlockNumberAsync();

            var balanceBefore = await _fixture.Node.GetBalanceAsync(_fixture.RecipientAddress, blockBefore);
            var balanceAfter = await _fixture.Node.GetBalanceAsync(_fixture.RecipientAddress, blockAfter);

            Assert.True(balanceAfter > balanceBefore,
                $"Balance after ({balanceAfter}) should be greater than before ({balanceBefore})");
        }

        [Fact]
        public async Task GetBalance_ViaRpc_AtHistoricalBlock_ReturnsCorrectBalance()
        {
            var blockBeforeHex = new HexBigInteger(await _fixture.Node.GetBlockNumberAsync()).HexValue;

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("2000000000000000000")); // 2 ETH
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var balanceBeforeReq = new RpcRequestMessage(1, "eth_getBalance",
                _fixture.RecipientAddress, blockBeforeHex);
            var balanceBeforeResp = await _dispatcher.DispatchAsync(balanceBeforeReq);
            Assert.Null(balanceBeforeResp.Error);

            var balanceLatestReq = new RpcRequestMessage(2, "eth_getBalance",
                _fixture.RecipientAddress, "latest");
            var balanceLatestResp = await _dispatcher.DispatchAsync(balanceLatestReq);
            Assert.Null(balanceLatestResp.Error);

            var before = ParseHexBigInteger(balanceBeforeResp.Result);
            var latest = ParseHexBigInteger(balanceLatestResp.Result);

            Assert.True(latest > before,
                $"Latest balance ({latest}) should be greater than historical ({before})");
        }

        [Fact]
        public async Task ERC20BalanceOf_ViaEthCall_AtHistoricalBlock()
        {
            var mintAmount = BigInteger.Parse("1000000000000000000000"); // 1000 tokens
            var contractAddress = await _fixture.DeployERC20Async(mintAmount);

            var blockAfterMint = await _fixture.Node.GetBlockNumberAsync();

            var transferResult = await _fixture.TransferERC20Async(
                contractAddress, _fixture.RecipientAddress, 500);
            Assert.True(transferResult.Success);

            var blockAfterTransfer = await _fixture.Node.GetBlockNumberAsync();

            var balanceAtMint = await GetERC20BalanceViaRpc(
                contractAddress, _fixture.Address, new HexBigInteger(blockAfterMint).HexValue);
            Assert.Equal(mintAmount, balanceAtMint);

            var balanceAfterTransfer = await GetERC20BalanceViaRpc(
                contractAddress, _fixture.Address, "latest");
            Assert.Equal(mintAmount - 500, balanceAfterTransfer);

            var recipientAtMint = await GetERC20BalanceViaRpc(
                contractAddress, _fixture.RecipientAddress, new HexBigInteger(blockAfterMint).HexValue);
            Assert.Equal(BigInteger.Zero, recipientAtMint);

            var recipientAfterTransfer = await GetERC20BalanceViaRpc(
                contractAddress, _fixture.RecipientAddress, "latest");
            Assert.Equal(500, recipientAfterTransfer);
        }

        [Fact]
        public async Task GetTransactionCount_AtHistoricalBlock_ReturnsCorrectNonce()
        {
            var blockBefore = await _fixture.Node.GetBlockNumberAsync();
            var nonceBefore = await _fixture.Node.GetNonceAsync(_fixture.Address, blockBefore);

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress, BigInteger.Parse("100000000000000000")); // 0.1 ETH
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var blockAfter = await _fixture.Node.GetBlockNumberAsync();
            var nonceAfter = await _fixture.Node.GetNonceAsync(_fixture.Address, blockAfter);

            Assert.Equal(nonceBefore + 1, nonceAfter);

            var nonceHistorical = await _fixture.Node.GetNonceAsync(_fixture.Address, blockBefore);
            Assert.Equal(nonceBefore, nonceHistorical);
        }

        [Fact]
        public async Task GetTransactionCount_ViaRpc_AtHistoricalBlock()
        {
            var blockBeforeHex = new HexBigInteger(await _fixture.Node.GetBlockNumberAsync()).HexValue;

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress, BigInteger.Parse("100000000000000000"));
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var nonceBeforeReq = new RpcRequestMessage(1, "eth_getTransactionCount",
                _fixture.Address, blockBeforeHex);
            var nonceBeforeResp = await _dispatcher.DispatchAsync(nonceBeforeReq);
            Assert.Null(nonceBeforeResp.Error);

            var nonceLatestReq = new RpcRequestMessage(2, "eth_getTransactionCount",
                _fixture.Address, "latest");
            var nonceLatestResp = await _dispatcher.DispatchAsync(nonceLatestReq);
            Assert.Null(nonceLatestResp.Error);

            var before = ParseHexBigInteger(nonceBeforeResp.Result);
            var latest = ParseHexBigInteger(nonceLatestResp.Result);

            Assert.True(latest > before,
                $"Latest nonce ({latest}) should be greater than historical ({before})");
        }

        [Fact]
        public async Task GetCode_BeforeDeployment_ReturnsEmpty()
        {
            var blockBeforeDeploy = await _fixture.Node.GetBlockNumberAsync();

            var contractAddress = await _fixture.DeployERC20Async();

            var codeAtLatest = await _fixture.Node.GetCodeAsync(contractAddress);
            Assert.NotNull(codeAtLatest);
            Assert.True(codeAtLatest.Length > 0);

            var codeBeforeDeploy = await _fixture.Node.GetCodeAsync(contractAddress, blockBeforeDeploy);
            Assert.True(codeBeforeDeploy == null || codeBeforeDeploy.Length == 0,
                "Code should not exist before contract deployment");
        }

        [Fact]
        public async Task GetCode_ViaRpc_BeforeDeployment_Returns0x()
        {
            var blockBeforeHex = new HexBigInteger(await _fixture.Node.GetBlockNumberAsync()).HexValue;

            var contractAddress = await _fixture.DeployERC20Async();

            var codeLatestReq = new RpcRequestMessage(1, "eth_getCode", contractAddress, "latest");
            var codeLatestResp = await _dispatcher.DispatchAsync(codeLatestReq);
            Assert.Null(codeLatestResp.Error);
            var codeLatest = codeLatestResp.Result?.ToString();
            Assert.NotEqual("0x", codeLatest);

            var codeBeforeReq = new RpcRequestMessage(2, "eth_getCode", contractAddress, blockBeforeHex);
            var codeBeforeResp = await _dispatcher.DispatchAsync(codeBeforeReq);
            Assert.Null(codeBeforeResp.Error);
            var codeBefore = codeBeforeResp.Result?.ToString();
            Assert.Equal("0x", codeBefore);
        }

        [Fact]
        public async Task GetStorageAt_AtHistoricalBlock_ReturnsOldValue()
        {
            var mintAmount = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(mintAmount);

            var blockAfterMint = await _fixture.Node.GetBlockNumberAsync();

            var transferResult = await _fixture.TransferERC20Async(
                contractAddress, _fixture.RecipientAddress, 500);
            Assert.True(transferResult.Success);

            var totalSupplySlot = BigInteger.Zero;
            var storageAtMint = await _fixture.Node.GetStorageAtAsync(
                contractAddress, totalSupplySlot, blockAfterMint);
            var storageAtLatest = await _fixture.Node.GetStorageAtAsync(
                contractAddress, totalSupplySlot);

            var mintHex = storageAtMint?.ToHex() ?? "0x";
            var latestHex = storageAtLatest?.ToHex() ?? "0x";
            Assert.Equal(mintHex, latestHex);
        }

        [Fact]
        public async Task MultipleBlocks_ProgressiveBalanceChanges()
        {
            var recipient = _fixture.RecipientAddress;

            var block0 = await _fixture.Node.GetBlockNumberAsync();
            var balance0 = await _fixture.Node.GetBalanceAsync(recipient, block0);

            var tx1 = _fixture.CreateSignedTransaction(recipient, BigInteger.Parse("1000000000000000000"));
            var r1 = await _fixture.Node.SendTransactionAsync(tx1);
            Assert.True(r1.Success);
            var block1 = await _fixture.Node.GetBlockNumberAsync();

            var tx2 = _fixture.CreateSignedTransaction(recipient, BigInteger.Parse("2000000000000000000"));
            var r2 = await _fixture.Node.SendTransactionAsync(tx2);
            Assert.True(r2.Success);
            var block2 = await _fixture.Node.GetBlockNumberAsync();

            var tx3 = _fixture.CreateSignedTransaction(recipient, BigInteger.Parse("3000000000000000000"));
            var r3 = await _fixture.Node.SendTransactionAsync(tx3);
            Assert.True(r3.Success);
            var block3 = await _fixture.Node.GetBlockNumberAsync();

            var balanceAtBlock0 = await _fixture.Node.GetBalanceAsync(recipient, block0);
            var balanceAtBlock1 = await _fixture.Node.GetBalanceAsync(recipient, block1);
            var balanceAtBlock2 = await _fixture.Node.GetBalanceAsync(recipient, block2);
            var balanceAtBlock3 = await _fixture.Node.GetBalanceAsync(recipient, block3);

            Assert.Equal(balance0, balanceAtBlock0);
            Assert.True(balanceAtBlock1 > balanceAtBlock0);
            Assert.True(balanceAtBlock2 > balanceAtBlock1);
            Assert.True(balanceAtBlock3 > balanceAtBlock2);

            Assert.Equal(balanceAtBlock1 - balanceAtBlock0, BigInteger.Parse("1000000000000000000"));
            Assert.Equal(balanceAtBlock2 - balanceAtBlock1, BigInteger.Parse("2000000000000000000"));
            Assert.Equal(balanceAtBlock3 - balanceAtBlock2, BigInteger.Parse("3000000000000000000"));
        }

        [Fact]
        public async Task EthCall_AtEarliestBlock_Returns0()
        {
            var mintAmount = BigInteger.Parse("1000000000000000000000");
            var contractAddress = await _fixture.DeployERC20Async(mintAmount);

            var balanceAtEarliest = await GetERC20BalanceViaRpc(
                contractAddress, _fixture.Address, "earliest");
            Assert.Equal(BigInteger.Zero, balanceAtEarliest);
        }

        private async Task<BigInteger> GetERC20BalanceViaRpc(string contractAddress, string owner, string blockTag)
        {
            var balanceOfFunction = new BalanceOfFunction { Account = owner };
            var callData = balanceOfFunction.GetCallData();

            var callInput = JObject.FromObject(new
            {
                from = _fixture.Address,
                to = contractAddress,
                data = callData.ToHex(true)
            });

            var request = new RpcRequestMessage(1, "eth_call", callInput, blockTag);
            var response = await _dispatcher.DispatchAsync(request);

            if (response.Error != null)
                return BigInteger.Zero;

            var resultHex = response.Result?.ToString();
            if (string.IsNullOrEmpty(resultHex) || resultHex == "0x")
                return BigInteger.Zero;

            var decoder = new FunctionCallDecoder();
            var output = decoder.DecodeFunctionOutput<BalanceOfOutputDTO>(resultHex);
            return output.ReturnValue1;
        }

        private static BigInteger ParseHexBigInteger(object result)
        {
            var hex = result?.ToString();
            if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
            return hex.HexToBigInteger(false);
        }
    }
}
