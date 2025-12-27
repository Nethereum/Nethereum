using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Events;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.TokenServices.IntegrationTests
{
    public class TokenEventScannerTests
    {
        private readonly ITestOutputHelper _output;

        private const string VitalikAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
        private const string BaseRpcUrl = "https://mainnet.base.org";
        private const string EthereumRpcUrl = "https://eth.llamarpc.com";
        private const long Base = 8453;
        private const long EthereumMainnet = 1;

        public TokenEventScannerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ScanTransferEvents_FindsRecentTransfers()
        {
            var web3 = new Web3.Web3(BaseRpcUrl);
            var scanner = new Erc20EventScanner();

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var fromBlock = currentBlock.Value - 10000;

            _output.WriteLine($"Scanning from block {fromBlock} to {currentBlock.Value}");

            var result = await scanner.ScanTransferEventsAsync(
                web3,
                VitalikAddress,
                fromBlock);

            Assert.True(result.Success, $"Scan should succeed: {result.ErrorMessage}");
            Assert.NotNull(result.Transfers);
            Assert.NotNull(result.AffectedTokenAddresses);

            _output.WriteLine($"Found {result.Transfers.Count} transfers");
            _output.WriteLine($"Affected tokens: {result.AffectedTokenAddresses.Count}");

            foreach (var transfer in result.Transfers)
            {
                var direction = transfer.IsIncoming ? "IN" : "OUT";
                _output.WriteLine($"  [{direction}] {transfer.TokenAddress}: {transfer.Value} (block {transfer.BlockNumber})");
            }
        }

        [Fact]
        public async Task GetAffectedTokenAddresses_ReturnsDistinctAddresses()
        {
            var web3 = new Web3.Web3(BaseRpcUrl);
            var scanner = new Erc20EventScanner();

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var fromBlock = currentBlock.Value - 5000;

            var addresses = await scanner.GetAffectedTokenAddressesAsync(
                web3,
                VitalikAddress,
                fromBlock);

            Assert.NotNull(addresses);

            _output.WriteLine($"Found {addresses.Count} distinct token addresses:");
            foreach (var addr in addresses)
            {
                _output.WriteLine($"  {addr}");
            }
        }

        [Fact]
        public async Task RefreshBalancesFromEvents_UpdatesAffectedTokens()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(BaseRpcUrl);

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var fromBlock = currentBlock.Value - 10000;

            _output.WriteLine($"Refreshing balances from events since block {fromBlock}");

            var balances = await tokenService.RefreshBalancesFromEventsAsync(
                web3,
                VitalikAddress,
                Base,
                fromBlock,
                existingTokens: null,
                vsCurrency: "usd");

            Assert.NotNull(balances);

            _output.WriteLine($"Refreshed {balances.Count} token balances:");
            foreach (var balance in balances)
            {
                var priceStr = balance.Price.HasValue ? $"@ ${balance.Price:N4}" : "";
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N6} {priceStr}");
            }
        }

        [Fact]
        public async Task ScanTransferEvents_ViaMainService()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(BaseRpcUrl);

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var fromBlock = currentBlock.Value - 5000;

            var result = await tokenService.ScanTransferEventsAsync(
                web3,
                VitalikAddress,
                fromBlock);

            Assert.True(result.Success);
            Assert.NotNull(result.LatestBlockScanned);

            _output.WriteLine($"Scanned up to block {result.LatestBlockScanned}");
            _output.WriteLine($"Found {result.Transfers.Count} transfers affecting {result.AffectedTokenAddresses.Count} tokens");
        }
    }
}
