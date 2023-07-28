
using Nethereum.DataServices.Etherscan.Responses;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.DataServices.Etherscan.IntegrationTests
{
    public class EtherscanApiServiceTests
    {
        private static readonly SemaphoreSlim throttler = new SemaphoreSlim(1);

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

                var etherscanService = new EtherscanApiService();
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

                var etherscanService = new EtherscanApiService();
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

                var etherscanService = new EtherscanApiService();
                var result = await etherscanService.Contracts.GetSourceCodeAsync("0xBB9bc244D798123fDe783fCc1C72d3Bb8C189413");
                Assert.Equal("1", result.Status);
            });
        }

        [Fact]
        public async void ShouldGetContractCompilationMetadata()
        {
            await ThrottleEtherscanCallAsync(async () =>
            {
                var etherscanService = new EtherscanApiService();
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
                var etherscanService = new EtherscanApiService();
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
                var etherscanService = new EtherscanApiService(EtherscanChain.Binance);
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
                var etherscanService = new EtherscanApiService(EtherscanChain.Optimism);
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
                var etherscanService = new EtherscanApiService();
                var result = await etherscanService.Accounts.GetAccountInternalTransactionsAsync("0x2c1ba59d6f58433fb1eaee7d20b26ed83bda51a3", 1, 12, EtherscanResultSort.Descending);
                Assert.Equal("1", result.Status);
                Assert.Equal(12, result.Result.Count);

            });
        }

    }
}