using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
    public partial class SourcifyApiServiceTests
    {

        [Fact]
        public async void ShouldGetContractCompilationMetadata()
        {
            var sourcifyApiService = new Sourcify.SourcifyApiService();
            var compilationMetadata = await sourcifyApiService.GetCompilationMetadataAsync(1, "0x00000000219ab540356cBB839Cbe05303d7705Fa");
        }

        [Fact]
        public async void ShouldGetContractSources()
        {
            var sourcifyApiService = new Sourcify.SourcifyApiService();
            var files = await sourcifyApiService.GetSourceFilesFullMatchAsync(1, "0x00000000219ab540356cBB839Cbe05303d7705Fa");
        }
    }

}
