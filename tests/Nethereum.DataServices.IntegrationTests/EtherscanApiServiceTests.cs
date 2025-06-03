

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Nethereum.DataServices.Etherscan.Responses.Contract;
using System.Runtime.Serialization;

namespace Nethereum.DataServices.Etherscan.IntegrationTests
{
    public class EtherscanApiServiceTests
    {
        //NOTE: Etherscan has a rate limit of 5 calls per second
        //You need to enter your own API key
        private static readonly SemaphoreSlim throttler = new SemaphoreSlim(1);
        private string apiKey = "";
       
        public async Task ThrottleEtherscanCallAsync(Func<Task> action)
        {
            await throttler.WaitAsync();
            try
            {
                await action();
                await Task.Delay(5000);
            }
            finally
            {
                throttler.Release();
            }

        }

        [Fact]
        public async void ShouldGetAbi()
        {
           await ThrottleEtherscanCallAsync(async () =>
            {

                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Contracts.GetAbiAsync("0xBB9bc244D798123fDe783fCc1C72d3Bb8C189413");
                Assert.Equal("1", result.Status);
            }
            );

              
        }

        [Fact]
        public async void ShouldGetContractCreators()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {

                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Contracts.GetContractCreatorAndCreationTxHashAsync("0xB83c27805aAcA5C7082eB45C868d955Cf04C337F","0x68b3465833fb72A70ecDF485E0e4C7bD8665Fc45");
                Assert.Equal("1", result.Status);
            }
             );

        }

        [Fact]
        public async void ShouldGetContract()  
        {
            await ThrottleEtherscanCallAsync(async () =>
            {

                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Contracts.GetSourceCodeAsync("0xBB9bc244D798123fDe783fCc1C72d3Bb8C189413");
                Assert.Equal("1", result.Status);
            });
        }

        [Fact]
        public async void ShouldGetContractCompilationMetadata()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Contracts.GetSourceCodeAsync("0xC36442b4a4522E871399CD717aBDD847Ab11FE88");
                Assert.Equal("1", result.Status);
                var contract = result.Result.First();
                Assert.True(contract.ContainsSourceCodeCompilationMetadata());
                var compilationMetadata = contract.DeserialiseCompilationMetadata();
                var contractSource = compilationMetadata.GetLocalSourceCode(contract.ContractName);
                Assert.NotNull(contractSource);
            });
        }

        [Fact]
        public async void ShouldGetAccountTransactions()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetAccountTransactionsAsync("0xc5102fE9359FD9a28f877a67E36B0F050d81a3CC");
                Assert.Equal("1", result.Status);
                Assert.Equal(10, result.Result.Count);
               
            });
        }

        [Fact]
        public async void ShouldGetAccountTransactionsBinance()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(56, apiKey);
                var result = await etherscanService.Accounts.GetAccountTransactionsAsync("0x0E09FaBB73Bd3Ade0a17ECC321fD13a19e81cE82");
                Assert.Equal("1", result.Status);
                Assert.Equal(10, result.Result.Count);

            });
        }

        [Fact]
        public async void ShouldGetAccountTransactionsOptimism()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(10, apiKey);
                var result = await etherscanService.Accounts.GetAccountTransactionsAsync("0x6fd9d7AD17242c41f7131d257212c54A0e816691");
                Assert.Equal("1", result.Status);
                Assert.Equal(10, result.Result.Count);

            });
        }

        //

        [Fact]
        public async void ShouldGetAccountInternalTransactions()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetAccountInternalTransactionsAsync("0x2c1ba59d6f58433fb1eaee7d20b26ed83bda51a3", 1, 12, EtherscanResultSort.Descending);
                Assert.Equal("1", result.Status);
                Assert.Equal(12, result.Result.Count);

            });
        }

        [Fact]
        public async void ShouldGetBalance()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetBalanceAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
                Assert.Equal("1", result.Status);
                Assert.NotNull(result.Result);
            });
        }

        [Fact]
        public async void ShouldGetMultipleBalances()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetBalancesAsync(new[]
                {
            "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe",
            "0x742d35Cc6634C0532925a3b844Bc454e4438f44e" // another known address
        });
                Assert.Equal("1", result.Status);
                Assert.Equal(2, result.Result.Count);
            });
        }

        [Fact]
        public async void ShouldGetTokenTransfers()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetTokenTransfersAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
                Assert.Equal("1", result.Status);
                Assert.NotEmpty(result.Result);
            });
        }

        [Fact]
        public async void ShouldGetErc721Transfers()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetErc721TransfersAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
                Assert.Equal("1", result.Status);
                Assert.NotEmpty(result.Result);
            });
        }

        [Fact]
        public async void ShouldGetErc1155Transfers()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetErc1155TransfersAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
                Assert.Equal("1", result.Status);
                Assert.NotEmpty(result.Result);
            });
        }

        [Fact]
        public async void ShouldGetFundedBy()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetFundedByAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
                Assert.Equal("1", result.Status);
                Assert.NotNull(result.Result);
            });
        }

        [Fact(Skip ="You need the api PRO to test this")]
        public async void ShouldGetHistoricalBalance()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.Accounts.GetHistoricalBalanceAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe", blockNumber: 8000000);
                Assert.Equal("1", result.Status);
                Assert.NotNull(result.Result);
            });
        }

        [Fact]
        public async Task ShouldGetGasOracle()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.GasTracker.GetGasOracleAsync();

                Assert.Equal("1", result.Status);
                Assert.NotNull(result.Result.SafeGasPrice);
                Assert.NotNull(result.Result.ProposeGasPrice);
                Assert.NotNull(result.Result.FastGasPrice);
                Assert.NotNull(result.Result.SuggestBaseFee);
            });
        }

        [Fact]
        public async Task ShouldEstimateConfirmationTime()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService(1, apiKey);
                var result = await etherscanService.GasTracker.GetEstimatedConfirmationTimeAsync(2_000_000_000); // 2 Gwei

                Assert.Equal("1", result.Status);
                Assert.True(int.TryParse(result.Result, out var seconds));
                Assert.True(seconds > 0);
            });
        }


    }
}