using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
    public class SourcifyApiServiceTests
    {

        [Fact]
        public async void ShouldGetContractCompilationMetadata()
        {
            var sourcifyApiService = new Sourcify.SourcifyApiService();
            var compilationMetadata = await sourcifyApiService.GetCompilationMetadataAsync(1, "0xC36442b4a4522E871399CD717aBDD847Ab11FE88");
        }

        [Fact]
        public async void ShouldGetContractSources()
        {
            var sourcifyApiService = new Sourcify.SourcifyApiService();
            var files = await sourcifyApiService.GetSourceFilesFullMatchAsync(1, "0xC36442b4a4522E871399CD717aBDD847Ab11FE88");
        }
    }
}
