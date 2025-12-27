using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.Caching;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.TokenServices.IntegrationTests
{
    public class Erc20TokenServiceTests
    {
        private readonly ITestOutputHelper _output;

        private const string VitalikAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
        private const string TestAddress = "0x727e0b4DACE39C984eeFe342875323de5662Aa07";

        private const long EthereumMainnet = 1;
        private const long Optimism = 10;
        private const long BNBChain = 56;
        private const long Gnosis = 100;
        private const long Base = 8453;
        private const long Arbitrum = 42161;

        private static readonly Dictionary<long, string> ChainRpcUrls = new()
        {
            [EthereumMainnet] = "https://eth.llamarpc.com",
            [Optimism] = "https://mainnet.optimism.io",
            [BNBChain] = "https://bsc-dataseed.binance.org",
            [Gnosis] = "https://rpc.gnosischain.com",
            [Base] = "https://mainnet.base.org",
            [Arbitrum] = "https://arb1.arbitrum.io/rpc",
        };

        private static readonly Dictionary<long, string> ChainNames = new()
        {
            [EthereumMainnet] = "Ethereum Mainnet",
            [Optimism] = "Optimism",
            [BNBChain] = "BNB Chain",
            [Gnosis] = "Gnosis",
            [Base] = "Base",
            [Arbitrum] = "Arbitrum One",
        };

        public Erc20TokenServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetTokenList_ReturnsTokensForEthereumMainnet()
        {
            var tokenService = new Erc20TokenService();

            var tokens = await tokenService.GetTokenListAsync(EthereumMainnet);

            Assert.NotNull(tokens);
            Assert.True(tokens.Count > 0, "Should return tokens for Ethereum mainnet");

            var usdcToken = tokens.FirstOrDefault(t =>
                t.Symbol?.Equals("USDC", StringComparison.OrdinalIgnoreCase) == true);
            Assert.NotNull(usdcToken);

            _output.WriteLine($"Found {tokens.Count} tokens for Ethereum mainnet");
            _output.WriteLine($"USDC address: {usdcToken?.Address}");
        }

        [Theory]
        [InlineData(EthereumMainnet)]
        [InlineData(Optimism)]
        [InlineData(Base)]
        [InlineData(Arbitrum)]
        public async Task GetTokenList_ReturnsTokensForChain(long chainId)
        {
            var tokenService = new Erc20TokenService();
            var chainName = ChainNames.GetValueOrDefault(chainId, $"Chain {chainId}");

            var tokens = await tokenService.GetTokenListAsync(chainId);

            _output.WriteLine($"{chainName} (chainId={chainId}): Found {tokens?.Count ?? 0} tokens");

            Assert.NotNull(tokens);
            Assert.True(tokens.Count > 0, $"{chainName} should have tokens");
        }

        [Fact]
        public async Task SupportsChain_ReturnsTrueForEthereum()
        {
            var tokenService = new Erc20TokenService();

            var supported = await tokenService.SupportsChainAsync(EthereumMainnet);

            Assert.True(supported, "Ethereum mainnet should be supported");
        }

        [Fact]
        public async Task GetAllBalances_ReturnsVitalikBalances()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[EthereumMainnet]);
            var nativeConfig = NativeTokenConfig.ForChain(EthereumMainnet, "ETH", "Ether");

            var balances = await tokenService.GetAllBalancesAsync(
                web3,
                VitalikAddress,
                EthereumMainnet,
                includeNative: true,
                nativeToken: nativeConfig);

            Assert.NotNull(balances);

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            Assert.NotNull(nativeBalance);
            Assert.True(nativeBalance.Balance > 0, "Vitalik should have ETH");

            var tokensWithBalance = balances.Where(b => b.Balance > 0).ToList();
            _output.WriteLine($"Found {tokensWithBalance.Count} tokens with balance");
            _output.WriteLine($"Native ETH: {nativeBalance.BalanceDecimal:N6}");

            foreach (var balance in tokensWithBalance.Where(b => !b.IsNative).Take(10))
            {
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N6}");
            }
        }

        [Fact]
        public async Task GetBalancesWithPrices_ReturnsBalancesAndPrices()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[EthereumMainnet]);
            var nativeConfig = NativeTokenConfig.ForChain(EthereumMainnet, "ETH", "Ether");

            var balances = await tokenService.GetBalancesWithPricesAsync(
                web3,
                VitalikAddress,
                EthereumMainnet,
                vsCurrency: "usd",
                includeNative: true,
                nativeToken: nativeConfig);

            Assert.NotNull(balances);

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            Assert.NotNull(nativeBalance);
            Assert.True(nativeBalance.Price.HasValue, "ETH should have a price");
            Assert.True(nativeBalance.Price > 0, "ETH price should be > 0");

            _output.WriteLine($"ETH Balance: {nativeBalance.BalanceDecimal:N6}");
            _output.WriteLine($"ETH Price: ${nativeBalance.Price:N2}");
            _output.WriteLine($"ETH Value: ${nativeBalance.Value:N2}");

            var tokensWithPrices = balances
                .Where(b => !b.IsNative && b.Balance > 0 && b.Price.HasValue && b.Price > 0)
                .OrderByDescending(b => b.Value)
                .ToList();

            _output.WriteLine($"\nTokens with prices ({tokensWithPrices.Count}):");
            foreach (var balance in tokensWithPrices.Take(10))
            {
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N4} @ ${balance.Price:N4} = ${balance.Value:N2}");
            }
        }

        [Theory]
        [InlineData(EthereumMainnet)]
        [InlineData(Base)]
        [InlineData(Arbitrum)]
        public async Task GetAllBalances_MultiChain(long chainId)
        {
            var tokenService = new Erc20TokenService();
            var rpcUrl = ChainRpcUrls[chainId];
            var chainName = ChainNames.GetValueOrDefault(chainId, $"Chain {chainId}");
            var web3 = new Web3.Web3(rpcUrl);
            var nativeConfig = NativeTokenConfig.ForChain(chainId, "ETH", "Ether");

            _output.WriteLine($"=== {chainName} ===");

            var balances = await tokenService.GetAllBalancesAsync(
                web3,
                VitalikAddress,
                chainId,
                includeNative: true,
                nativeToken: nativeConfig);

            Assert.NotNull(balances);

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            _output.WriteLine($"Native: {nativeBalance?.BalanceDecimal:N6}");

            var tokensWithBalance = balances.Where(b => !b.IsNative && b.Balance > 0).Take(5);
            foreach (var balance in tokensWithBalance)
            {
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N6}");
            }
        }

        [Fact]
        public async Task InitializeCache_PreloadsTokenLists()
        {
            var cache = new MemoryCacheProvider();
            var tokenService = new Erc20TokenService(cacheProvider: cache);

            var chainIds = new[] { EthereumMainnet, Base };

            await tokenService.InitializeCacheAsync(chainIds);

            foreach (var chainId in chainIds)
            {
                var cacheKey = $"tokenlist:{chainId}";
                var exists = await cache.ExistsAsync(cacheKey);
                Assert.True(exists, $"Token list for chain {chainId} should be cached");
            }
        }

        [Fact]
        public async Task GetAllBalances_BNBChain()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[BNBChain]);
            var nativeConfig = NativeTokenConfig.ForChain(BNBChain, "BNB", "BNB");

            _output.WriteLine("=== BNB Chain ===");

            var tokens = await tokenService.GetTokenListAsync(BNBChain);
            _output.WriteLine($"Token list count: {tokens?.Count ?? 0}");

            var balances = await tokenService.GetAllBalancesAsync(
                web3,
                VitalikAddress,
                BNBChain,
                includeNative: true,
                nativeToken: nativeConfig);

            Assert.NotNull(balances);

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            _output.WriteLine($"Native BNB: {nativeBalance?.BalanceDecimal:N6}");

            var tokensWithBalance = balances.Where(b => !b.IsNative && b.Balance > 0).Take(10).ToList();
            _output.WriteLine($"Tokens with balance: {tokensWithBalance.Count}");
            foreach (var balance in tokensWithBalance)
            {
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N6}");
            }
        }

        [Fact]
        public async Task GetBalancesWithPrices_BNBChain()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[BNBChain]);
            var nativeConfig = NativeTokenConfig.ForChain(BNBChain, "BNB", "BNB");

            _output.WriteLine("=== BNB Chain with Prices ===");

            var balances = await tokenService.GetBalancesWithPricesAsync(
                web3,
                VitalikAddress,
                BNBChain,
                vsCurrency: "usd",
                includeNative: true,
                nativeToken: nativeConfig);

            Assert.NotNull(balances);

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            Assert.NotNull(nativeBalance);

            _output.WriteLine($"Native BNB Balance: {nativeBalance.BalanceDecimal:N6}");
            _output.WriteLine($"BNB Price: ${nativeBalance.Price:N2}");
            _output.WriteLine($"BNB Value: ${nativeBalance.Value:N2}");

            var tokensWithPrices = balances
                .Where(b => !b.IsNative && b.Balance > 0 && b.Price.HasValue && b.Price > 0)
                .OrderByDescending(b => b.Value)
                .ToList();

            _output.WriteLine($"\nTokens with prices ({tokensWithPrices.Count}):");
            foreach (var balance in tokensWithPrices.Take(10))
            {
                _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N4} @ ${balance.Price:N4} = ${balance.Value:N2}");
            }
        }

        [Fact]
        public async Task GetAllBalances_BNBChain_BatchProcessing()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[BNBChain]);

            _output.WriteLine("=== BNB Chain Batch Processing Test ===");

            var tokens = await tokenService.GetTokenListAsync(BNBChain);
            _output.WriteLine($"Total tokens from CoinGecko: {tokens?.Count ?? 0}");

            if (tokens == null || tokens.Count == 0)
            {
                _output.WriteLine("No tokens found, skipping batch test");
                return;
            }

            var testTokenCount = Math.Min(200, tokens.Count);
            var testTokens = tokens.Take(testTokenCount).ToList();
            _output.WriteLine($"Testing with {testTokenCount} tokens to verify batch processing...");

            try
            {
                var balances = await tokenService.GetBalancesForTokensAsync(
                    web3,
                    VitalikAddress,
                    testTokens);

                Assert.NotNull(balances);
                _output.WriteLine($"Successfully retrieved {balances.Count} balances");

                var tokensWithBalance = balances.Where(b => b.Balance > 0).ToList();
                _output.WriteLine($"Tokens with non-zero balance: {tokensWithBalance.Count}");

                foreach (var balance in tokensWithBalance.Take(10))
                {
                    _output.WriteLine($"  {balance.Token?.Symbol ?? "?"}: {balance.BalanceDecimal:N6}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        [Fact]
        public async Task Ethereum_TokenListCount()
        {
            var tokenService = new Erc20TokenService();
            var tokens = await tokenService.GetTokenListAsync(1); // Ethereum mainnet
            _output.WriteLine($"Ethereum token count: {tokens?.Count ?? 0}");
            Assert.NotNull(tokens);
        }

        [Fact]
        public async Task BNBChain_DirectMulticallTest()
        {
            var web3 = new Web3.Web3(ChainRpcUrls[BNBChain]);

            _output.WriteLine("=== BNB Chain Direct Multicall Test ===");

            var testTokenAddresses = new[]
            {
                "0xe9e7CEA3DedcA5984780Bafc599bD69ADd087D56", // BUSD
                "0x55d398326f99059fF775485246999027B3197955", // USDT
                "0x8AC76a51cc950d9822D68b83fE1Ad97B32Cd580d", // USDC
                "0x2170Ed0880ac9A755fd29B2688956BD959F933F8", // WETH
                "0x7130d2A12B9BCbFAe4f2634d864A1Ee1Ce3Ead9c"  // BTCB
            };

            _output.WriteLine($"Testing with {testTokenAddresses.Length} known BSC tokens...");

            try
            {
                var balances = await web3.Eth.ERC20.GetAllTokenBalancesUsingMultiCallAsync(
                    VitalikAddress,
                    testTokenAddresses.ToList(),
                    100);

                _output.WriteLine($"Multicall returned {balances.Count} results");
                foreach (var balance in balances)
                {
                    _output.WriteLine($"  {balance.ContractAddress}: {Web3.Web3.Convert.FromWei(balance.Balance, 18):N6}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Multicall ERROR: {ex.GetType().Name}: {ex.Message}");

                _output.WriteLine("\nFalling back to batch RPC...");
                try
                {
                    var balances = await web3.Eth.ERC20.GetAllTokenBalancesUsingBatchRpcAsync(
                        new[] { VitalikAddress },
                        testTokenAddresses.ToList(),
                        100);

                    _output.WriteLine($"Batch RPC returned {balances.Count} results");
                    foreach (var balance in balances)
                    {
                        _output.WriteLine($"  {balance.ContractAddress}: {Web3.Web3.Convert.FromWei(balance.Balance, 18):N6}");
                    }
                }
                catch (Exception ex2)
                {
                    _output.WriteLine($"Batch RPC ERROR: {ex2.GetType().Name}: {ex2.Message}");
                    throw;
                }
            }
        }

        [Fact]
        public async Task GetBalancesForTokens_WithSpecificTokens()
        {
            var tokenService = new Erc20TokenService();
            var web3 = new Web3.Web3(ChainRpcUrls[EthereumMainnet]);

            var specificTokens = new[]
            {
                new ERC20.Models.TokenInfo
                {
                    Address = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
                    Symbol = "USDC",
                    Name = "USD Coin",
                    Decimals = 6,
                    ChainId = EthereumMainnet
                },
                new ERC20.Models.TokenInfo
                {
                    Address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2",
                    Symbol = "WETH",
                    Name = "Wrapped Ether",
                    Decimals = 18,
                    ChainId = EthereumMainnet
                },
                new ERC20.Models.TokenInfo
                {
                    Address = "0x514910771AF9Ca656af840dff83E8264EcF986CA",
                    Symbol = "LINK",
                    Name = "ChainLink",
                    Decimals = 18,
                    ChainId = EthereumMainnet
                }
            };

            var balances = await tokenService.GetBalancesForTokensAsync(
                web3,
                VitalikAddress,
                specificTokens);

            Assert.NotNull(balances);
            Assert.Equal(specificTokens.Length, balances.Count);

            foreach (var balance in balances)
            {
                _output.WriteLine($"{balance.Token?.Symbol}: {balance.BalanceDecimal:N6}");
            }
        }
    }
}
