using Nethereum.Generators.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests
{
    public class ConfigSetGeneratorTest
    {
        [Fact]
        public void ShouldGenerateFilesFromConfigSet()
        {
            // Arrange
            var jsonGeneratorSetsExample2 = @"
        [
            {
                ""paths"": [""examples/testAbi/out/ERC20.sol/Standard_Token.json""],
                ""generatorConfigs"": [
                    {
                        ""baseNamespace"": ""MyProject.Contracts"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts"",
                        ""codeGenLang"": 0,
                        ""generatorType"": ""ContractDefinition""
                    },
                    {
                        ""baseNamespace"": ""MyProject.Contracts"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts"",
                        ""codeGenLang"": 0,
                        ""generatorType"": ""UnityRequest""
                    }
                ]
            },
            {
                ""paths"": [""examples/testAbi/out/IncrementSystem.sol/IncrementSystem.json""],
                ""generatorConfigs"": [
                    {
                        ""baseNamespace"": ""MyProject.Contracts.MyWorld1.Systems"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems"",
                        ""codeGenLang"": 0,
                        ""generatorType"": ""ContractDefinition"",
                        ""mudNamespace"": ""myworld1""
                    },
                    {
                        ""baseNamespace"": ""MyProject.Contracts.MyWorld1.Systems"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems"",
                        ""codeGenLang"": 0,
                        ""generatorType"": ""MudExtendedService"",
                        ""mudNamespace"": ""myworld1""
                    }
                ]
            },
            {
                ""paths"": [""examples/testAbi/mudMultipleNamespace/mud.config.ts""],
                ""generatorConfigs"": [
                    {
                        ""baseNamespace"": ""MyProject.Contracts.MyWorld1.Tables"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Tables"",
                        ""generatorType"": ""MudTables"",
                        ""mudNamespace"": ""myworld1""
                    }
                ]
            },
            {
                ""paths"": [""examples/testAbi/mudMultipleNamespace/mud.config.ts""],
                ""generatorConfigs"": [
                    {
                        ""baseNamespace"": ""MyProject.Contracts.MyWorld2.Tables"",
                        ""basePath"": ""codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld2/Tables"",
                        ""generatorType"": ""MudTables"",
                        ""mudNamespace"": ""myworld2""
                    }
                ]
            }
        ]";

            var rootPath = AppContext.BaseDirectory; // Output directory where files are copied during the build

            var processor = new GeneratorSetProcessor();

            // Act
            var generatedFiles = processor.GenerateFilesFromConfigJsonString(jsonGeneratorSetsExample2, rootPath);

            // Assert
            Assert.NotEmpty(generatedFiles);

        }
    }
}
