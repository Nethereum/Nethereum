using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Pricing;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.TokenServices.IntegrationTests
{
    public class PricingTests
    {
        private readonly ITestOutputHelper _output;

        private const long EthereumMainnet = 1;
        private const long Base = 8453;

        public PricingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetPrices_ReturnsEthereumPrice()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var prices = await priceProvider.GetPricesAsync(new[] { "ethereum" }, "usd");

            Assert.NotNull(prices);
            Assert.True(prices.ContainsKey("ethereum"));
            Assert.True(prices["ethereum"].Price > 0);

            _output.WriteLine($"ETH price: ${prices["ethereum"].Price:N2}");
        }

        [Fact]
        public async Task GetPrices_ReturnsBatchPrices()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var ids = new[] { "ethereum", "bitcoin", "usd-coin", "tether" };
            var prices = await priceProvider.GetPricesAsync(ids, "usd");

            Assert.NotNull(prices);
            Assert.True(prices.Count > 0);

            foreach (var (id, price) in prices)
            {
                _output.WriteLine($"{id}: ${price.Price:N2}");
            }
        }

        [Fact]
        public async Task GetNativeTokenPrice_ReturnsPrice()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var ethPrice = await priceProvider.GetNativeTokenPriceAsync(EthereumMainnet, "usd");

            Assert.NotNull(ethPrice);
            Assert.True(ethPrice.Price > 0);

            _output.WriteLine($"Ethereum native price: ${ethPrice.Price:N2}");

            var basePrice = await priceProvider.GetNativeTokenPriceAsync(Base, "usd");

            Assert.NotNull(basePrice);
            _output.WriteLine($"Base native price: ${basePrice.Price:N2}");
        }

        [Fact]
        public async Task GetPricesByContract_ReturnsUSDCPrice()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

            var prices = await priceProvider.GetPricesByContractAsync(
                EthereumMainnet,
                new[] { usdcAddress },
                "usd");

            Assert.NotNull(prices);
            Assert.True(prices.Count > 0);

            var usdcPrice = prices.Values.FirstOrDefault();
            Assert.NotNull(usdcPrice);
            Assert.True(usdcPrice.Price > 0.9m && usdcPrice.Price < 1.1m, "USDC should be around $1");

            _output.WriteLine($"USDC price: ${usdcPrice.Price:N4}");
        }

        [Fact]
        public async Task GetTokenId_FindsUSDC()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

            var tokenId = await priceProvider.GetTokenIdAsync(EthereumMainnet, usdcAddress);

            Assert.NotNull(tokenId);
            Assert.Equal("usd-coin", tokenId);

            _output.WriteLine($"USDC CoinGecko ID: {tokenId}");
        }

        [Fact]
        public async Task GetTokenIds_FindsMultipleTokens()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var addresses = new[]
            {
                "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC
                "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", // WETH
                "0x514910771AF9Ca656af840dff83E8264EcF986CA", // LINK
            };

            var ids = await priceProvider.GetTokenIdsAsync(EthereumMainnet, addresses);

            Assert.NotNull(ids);
            Assert.True(ids.Count > 0);

            foreach (var (addr, id) in ids)
            {
                _output.WriteLine($"{addr}: {id}");
            }
        }

        [Fact]
        public async Task GetPrices_CacheHit_ReturnsCachedData()
        {
            var priceProvider = new CoinGeckoPriceProvider();

            var prices1 = await priceProvider.GetPricesAsync(new[] { "ethereum" }, "usd");
            var prices2 = await priceProvider.GetPricesAsync(new[] { "ethereum" }, "usd");

            Assert.Equal(prices1["ethereum"].Price, prices2["ethereum"].Price);
        }
    }
}
