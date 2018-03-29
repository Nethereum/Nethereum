using System.Linq;
using Nethereum.Generators.Net;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests
{
    public class ContractProjectGeneratorTests
    {
        private readonly ContractProjectGenerator _contractProjectGenerator;

        public ContractProjectGeneratorTests()
        {
            var contractMetaData = TestContractsUtils.StandardContract;

            var contractABI = new GeneratorModelABIDeserialiser().DeserialiseABI(contractMetaData.ABI);

            _contractProjectGenerator = new ContractProjectGenerator(
                contractABI, 
                "StandardContract",
                contractMetaData.ByteCode,
                "ContractProjectGenerator.Tests", 
                "StandardContract.Service",
                "StandardContract.CQS",
                "StandardContract.DTO", 
                @"c:\Temp\",
                "\\");
        }

        [Fact]
        public void GeneratesAllExpectedFiles()
        {
            var expectedFiles = new[]
            {
                @"c:\Temp\StandardContract\CQS\StandardContractDeployment.cs",
                @"c:\Temp\StandardContract\CQS\NameFunction.cs",
                @"c:\Temp\StandardContract\CQS\ApproveFunction.cs",
                @"c:\Temp\StandardContract\CQS\TotalSupplyFunction.cs",
                @"c:\Temp\StandardContract\CQS\TransferFromFunction.cs",
                @"c:\Temp\StandardContract\CQS\BalancesFunction.cs",
                @"c:\Temp\StandardContract\CQS\DecimalsFunction.cs",
                @"c:\Temp\StandardContract\CQS\AllowedFunction.cs",
                @"c:\Temp\StandardContract\CQS\BalanceOfFunction.cs",
                @"c:\Temp\StandardContract\CQS\SymbolFunction.cs",
                @"c:\Temp\StandardContract\CQS\TransferFunction.cs",
                @"c:\Temp\StandardContract\CQS\AllowanceFunction.cs",
                @"c:\Temp\StandardContract\DTO\TransferEventDTO.cs",
                @"c:\Temp\StandardContract\DTO\ApprovalEventDTO.cs",
                @"c:\Temp\StandardContract\DTO\NameOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\TotalSupplyOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\BalancesOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\DecimalsOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\AllowedOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\BalanceOfOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\SymbolOutputDTO.cs",
                @"c:\Temp\StandardContract\DTO\AllowanceOutputDTO.cs",
                @"c:\Temp\StandardContract\Service\StandardContractService.cs"
            };

            var generatedFiles = _contractProjectGenerator.GenerateAll();

            Assert.Equal(expectedFiles.Length, generatedFiles.Length);
            Assert.All(expectedFiles, f => generatedFiles.Any(g => g.FileName == f));
        }

        [Fact]
        public void ShouldNotGenerateFilesWithEmptyContent()
        {
            var generatedFiles = _contractProjectGenerator.GenerateAll();
            Assert.DoesNotContain(generatedFiles, g => string.IsNullOrEmpty(g?.GeneratedCode));
        }
    }
}
