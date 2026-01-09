using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DataServices.ABIInfoStorage;
using Nethereum.DataServices.Sourcify;
using Nethereum.DataServices.Sourcify.Responses;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
    public partial class SourcifyApiServiceTests
    {
        private const string ETH2_DEPOSIT_CONTRACT = "0x00000000219ab540356cBB839Cbe05303d7705Fa";
        private const string USDC_CONTRACT = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        private const long MAINNET_CHAIN_ID = 1;

        [Fact]
        public async void ShouldGetContractCompilationMetadata()
        {
            var sourcifyApiService = new SourcifyApiService();
            var compilationMetadata = await sourcifyApiService.GetCompilationMetadataAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT);
        }

        [Fact]
        public async void ShouldGetContractSources()
        {
            var sourcifyApiService = new SourcifyApiService();
            var files = await sourcifyApiService.GetSourceFilesFullMatchAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT);
        }

        [Fact(Skip = "V2 API chains endpoint moved to different path")]
        public async void ShouldGetChains()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var chains = await sourcifyApiService.GetChainsAsync();
            Assert.NotEmpty(chains);
        }

        [Fact]
        public async Task ShouldGetContractV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var contract = await sourcifyApiService.GetContractAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT);

            Assert.NotNull(contract);
            Assert.Equal(MAINNET_CHAIN_ID, contract.ChainId);
            Assert.NotNull(contract.Match);
        }

        [Fact]
        public async Task ShouldGetContractWithAbiV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var contract = await sourcifyApiService.GetContractAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT, fields: "abi");

            Assert.NotNull(contract);
            Assert.NotNull(contract.Abi);
            var abiString = contract.GetAbiString();
            Assert.NotNull(abiString);
            Assert.Contains("function", abiString.ToLower());
        }

        [Fact]
        public async Task ShouldGetContractAbiOnlyV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var abi = await sourcifyApiService.GetContractAbiAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT);

            Assert.NotNull(abi);
            Assert.Contains("[", abi);
        }

        [Fact]
        public async Task ShouldGetContractWithCompilationInfoV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var contract = await sourcifyApiService.GetContractAsync(MAINNET_CHAIN_ID, ETH2_DEPOSIT_CONTRACT, fields: "abi,compilation");

            Assert.NotNull(contract);
            Assert.NotNull(contract.Compilation);
            Assert.NotNull(contract.Compilation.CompilerVersion);
        }

        [Fact]
        public async Task ShouldGetContractsListV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var response = await sourcifyApiService.GetContractsAsync(MAINNET_CHAIN_ID, limit: 10);

            Assert.NotNull(response);
            Assert.NotNull(response.Results);
            Assert.NotEmpty(response.Results);
            Assert.True(response.Results.Count <= 10);
        }

        [Fact]
        public async Task ShouldGetContractAllChainsV2()
        {
            var sourcifyApiService = new SourcifyApiServiceV2();
            var response = await sourcifyApiService.GetContractAllChainsAsync(ETH2_DEPOSIT_CONTRACT);

            Assert.NotNull(response);
            Assert.NotNull(response.Results);
            Assert.NotEmpty(response.Results);
        }
    }

    public class SourcifyABIInfoStorageTests
    {
        private const string USDC_CONTRACT = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        private const long MAINNET_CHAIN_ID = 1;

        [Fact]
        public async Task ShouldGetABIInfoFromSourcify()
        {
            var storage = new SourcifyABIInfoStorage();
            var abiInfo = await storage.GetABIInfoAsync(MAINNET_CHAIN_ID, USDC_CONTRACT);

            Assert.NotNull(abiInfo);
            Assert.NotNull(abiInfo.ABI);
            Assert.NotNull(abiInfo.ContractABI);
        }

        [Fact]
        public void ShouldFindFunctionABIFromSourcify()
        {
            var storage = new SourcifyABIInfoStorage();
            var transferSelector = "0xa9059cbb";

            var functionABI = storage.FindFunctionABIFromInputData(MAINNET_CHAIN_ID, USDC_CONTRACT, transferSelector + "0000000000000000000000000000000000000000000000000000000000000001");

            Assert.NotNull(functionABI);
            Assert.Equal("transfer", functionABI.Name);
        }

        [Fact]
        public void ShouldFindEventABIFromSourcify()
        {
            var storage = new SourcifyABIInfoStorage();
            var transferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

            var eventABI = storage.FindEventABI(MAINNET_CHAIN_ID, USDC_CONTRACT, transferEventSignature);

            Assert.NotNull(eventABI);
            Assert.Equal("Transfer", eventABI.Name);
        }
    }

    public class ABIInfoStorageFactoryTests
    {
        private const string USDC_CONTRACT = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        private const long MAINNET_CHAIN_ID = 1;

        [Fact]
        public void ShouldCreateDefaultStorage()
        {
            var storage = ABIInfoStorageFactory.CreateDefault();
            Assert.NotNull(storage);
        }

        [Fact]
        public void ShouldCreateSourcifyOnlyStorage()
        {
            var storage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            Assert.NotNull(storage);
        }

        [Fact]
        public void ShouldFindFunctionABIWithDefaultStorage()
        {
            var storage = ABIInfoStorageFactory.CreateDefault();
            var transferSelector = "0xa9059cbb";

            var functionABI = storage.FindFunctionABIFromInputData(MAINNET_CHAIN_ID, USDC_CONTRACT, transferSelector + "0000000000000000000000000000000000000000000000000000000000000001");

            Assert.NotNull(functionABI);
            Assert.Equal("transfer", functionABI.Name);
        }
    }

    public class Sourcify4ByteSignatureServiceTests
    {
        [Fact]
        public async Task ShouldLookupFunctionSignature()
        {
            var service = new Sourcify4ByteSignatureService();
            var response = await service.LookupFunctionAsync("0xa9059cbb");

            Assert.NotNull(response);
            Assert.True(response.Ok);
            Assert.NotNull(response.Result);
            Assert.NotNull(response.Result.Function);
            Assert.True(response.Result.Function.ContainsKey("0xa9059cbb"));

            var signatures = response.Result.Function["0xa9059cbb"];
            Assert.NotEmpty(signatures);
            Assert.Contains(signatures, s => s.Name == "transfer(address,uint256)");
        }

        [Fact]
        public async Task ShouldLookupEventSignature()
        {
            var service = new Sourcify4ByteSignatureService();
            var transferEventHash = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
            var response = await service.LookupEventAsync(transferEventHash);

            Assert.NotNull(response);
            Assert.True(response.Ok);
            Assert.NotNull(response.Result);
            Assert.NotNull(response.Result.Event);
            Assert.True(response.Result.Event.ContainsKey(transferEventHash));

            var signatures = response.Result.Event[transferEventHash];
            Assert.NotEmpty(signatures);
            Assert.Contains(signatures, s => s.Name == "Transfer(address,address,uint256)");
        }

        [Fact]
        public async Task ShouldLookupMultipleFunctionsAndEvents()
        {
            var service = new Sourcify4ByteSignatureService();

            var transferSelector = "0xa9059cbb";
            var approveSelector = "0x095ea7b3";
            var transferEventHash = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
            var approvalEventHash = "0x8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925";

            var response = await service.LookupAsync(
                functionSignatures: new[] { transferSelector, approveSelector },
                eventSignatures: new[] { transferEventHash, approvalEventHash });

            Assert.NotNull(response);
            Assert.True(response.Ok);
            Assert.NotNull(response.Result);

            Assert.NotNull(response.Result.Function);
            Assert.True(response.Result.Function.ContainsKey(transferSelector));
            Assert.True(response.Result.Function.ContainsKey(approveSelector));
            Assert.Contains(response.Result.Function[transferSelector], s => s.Name == "transfer(address,uint256)");
            Assert.Contains(response.Result.Function[approveSelector], s => s.Name == "approve(address,uint256)");

            Assert.NotNull(response.Result.Event);
            Assert.True(response.Result.Event.ContainsKey(transferEventHash));
            Assert.True(response.Result.Event.ContainsKey(approvalEventHash));
            Assert.Contains(response.Result.Event[transferEventHash], s => s.Name == "Transfer(address,address,uint256)");
            Assert.Contains(response.Result.Event[approvalEventHash], s => s.Name == "Approval(address,address,uint256)");
        }

        [Fact]
        public async Task ShouldSearchSignatures()
        {
            var service = new Sourcify4ByteSignatureService();
            var response = await service.SearchAsync("transfer");

            Assert.NotNull(response);
            Assert.True(response.Ok);
            Assert.NotNull(response.Result);
            Assert.True(response.Result.Function != null || response.Result.Event != null);
        }
    }

    public class SourcifyParquetExportServiceTests
    {
        [Fact]
        public async Task ShouldListFiles()
        {
            var service = new SourcifyParquetExportService();
            var response = await service.ListFilesAsync("v2/", null, 10);

            Assert.NotNull(response);
            Assert.NotNull(response.Contents);
            Assert.NotEmpty(response.Contents);
            Assert.True(response.Contents.Count <= 10);

            var firstFile = response.Contents.First();
            Assert.NotNull(firstFile.Key);
            Assert.NotNull(firstFile.ETag);
            Assert.True(firstFile.Size > 0);
        }

        [Fact]
        public async Task ShouldListTableFiles()
        {
            var service = new SourcifyParquetExportService();
            var files = await service.ListTableFilesAsync("signatures");

            Assert.NotNull(files);
            Assert.NotEmpty(files);
            Assert.All(files, f => Assert.Contains("signatures", f.Key));
            Assert.All(files, f => Assert.EndsWith(".parquet", f.Key));
        }

        [Fact]
        public async Task ShouldListAllFiles()
        {
            var service = new SourcifyParquetExportService();
            var files = await service.ListAllFilesAsync("v2/");

            Assert.NotNull(files);
            Assert.NotEmpty(files);

            var tables = files.Select(f => f.TableName).Distinct().ToList();
            Assert.True(tables.Count > 1);
        }

        [Fact]
        public async Task ShouldDownloadFileToStream()
        {
            var service = new SourcifyParquetExportService();
            var files = await service.ListFilesAsync("v2/signatures/", null, 1);

            Assert.NotEmpty(files.Contents);
            var fileToDownload = files.Contents.First();

            using (var stream = await service.DownloadFileAsync(fileToDownload.Key))
            {
                Assert.NotNull(stream);
                Assert.True(stream.CanRead);

                var buffer = new byte[4];
                var bytesRead = await stream.ReadAsync(buffer, 0, 4);
                Assert.Equal(4, bytesRead);
                Assert.Equal((byte)'P', buffer[0]);
                Assert.Equal((byte)'A', buffer[1]);
                Assert.Equal((byte)'R', buffer[2]);
                Assert.Equal((byte)'1', buffer[3]);
            }
        }
    }

    public class SourcifyABIInfoStorage4ByteFallbackTests
    {
        [Fact]
        public void ShouldFindFunctionABIFrom4ByteFallback()
        {
            var storage = new SourcifyABIInfoStorage();
            var transferSelector = "0xa9059cbb";

            var functionABI = storage.FindFunctionABI(transferSelector);

            Assert.NotNull(functionABI);
            Assert.NotEmpty(functionABI);
            Assert.Equal("transfer", functionABI[0].Name);
            Assert.Equal(2, functionABI[0].InputParameters.Length);
        }

        [Fact]
        public void ShouldFindFunctionABIFromInputDataWith4ByteFallback()
        {
            var storage = new SourcifyABIInfoStorage();
            var inputData = "0xa9059cbb0000000000000000000000000000000000000000000000000000000000000001";

            var functionABI = storage.FindFunctionABIFromInputData(inputData);

            Assert.NotNull(functionABI);
            Assert.NotEmpty(functionABI);
            Assert.Equal("transfer", functionABI[0].Name);
        }

        [Fact]
        public void ShouldFindEventABIFrom4ByteFallback()
        {
            var storage = new SourcifyABIInfoStorage();
            var transferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

            var eventABI = storage.FindEventABI(transferEventSignature);

            Assert.NotNull(eventABI);
            Assert.NotEmpty(eventABI);
            Assert.Equal("Transfer", eventABI[0].Name);
        }

        [Fact]
        public void SourcifyStorageReturnsEmptyForUnknownSelectors()
        {
            var storage = new SourcifyABIInfoStorage(
                new SourcifyApiServiceV2(),
                resolveProxies: false);
            var unknownSelector = "0xdeadbeef";

            var functionABI = storage.FindFunctionABI(unknownSelector);

            Assert.Empty(functionABI);
        }
    }
}
