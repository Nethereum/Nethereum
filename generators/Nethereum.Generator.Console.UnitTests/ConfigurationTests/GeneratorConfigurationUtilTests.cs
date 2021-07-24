using Nethereum.Generator.Console.Configuration;
using System.IO;
using Nethereum.Generators.Tests.Common.TestData;
using Xunit;
using Nethereum.Generators.Core;
using System;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests
{
    public class GeneratorConfigurationUtilTests
    {
        private readonly string _projectPath;

        public GeneratorConfigurationUtilTests()
        {
            _projectPath = Path.Combine(TestEnvironment.TempPath, "GeneratorConfigurationUtilTests");
        }

        [Fact]
        public void GetFullFileAndFolderPaths_GivenFolderPath_ReturnsOnlyFolderPath()
        {
            (string folder, string file) = GeneratorConfigurationUtils.GetFullFileAndFolderPaths(_projectPath);
            Assert.Equal(_projectPath, folder);
            Assert.Null(file);
        }

        [Theory]
        [InlineData("test.csproj", true)]
        [InlineData("test.fsproj", true)]
        [InlineData("test.vbproj", true)]
        [InlineData("test.xxx", false)]
        public void FindFirstProjectFile_FindExpectedFilesOrReturnsNull(string projectFileName, bool expectItToBeFound)
        {
            var expectedProjectFilePath = TestEnvironment.WriteFileToFolder(_projectPath, projectFileName, string.Empty);
            try
            {
                var firstFile = GeneratorConfigurationUtils.FindFirstProjectFile(_projectPath);
                if (expectItToBeFound)
                    Assert.Equal(expectedProjectFilePath, firstFile);
                else
                    Assert.Null(firstFile);
            }
            finally
            {
                File.Delete(expectedProjectFilePath);
            }
        }

        [Fact]
        public void FindFirstProjectFile_WillReturnsTheFirstOfManyMatchingProjectFiles()
        {
            var csProjFile = TestEnvironment.WriteFileToFolder(_projectPath, "test1.csproj", string.Empty);
            var fsProjFile = TestEnvironment.WriteFileToFolder(_projectPath, "test1.fsproj", string.Empty);
            var vbProjFile = TestEnvironment.WriteFileToFolder(_projectPath, "test1.vbproj", string.Empty);
            try
            {
                Assert.NotNull(GeneratorConfigurationUtils.FindFirstProjectFile(_projectPath));
                //we're not presently concerned which file was returned
            }
            finally
            {
                File.Delete(csProjFile);
                File.Delete(fsProjFile);
                File.Delete(vbProjFile);
            }
        }

        [Fact]
        public void DeriveCodeGenLanguage()
        {
            Assert.Equal(CodeGenLanguage.CSharp, GeneratorConfigurationUtils.DeriveCodeGenLanguage("test.csproj"));
            Assert.Equal(CodeGenLanguage.FSharp, GeneratorConfigurationUtils.DeriveCodeGenLanguage("test.fsproj"));
            Assert.Equal(CodeGenLanguage.Vb, GeneratorConfigurationUtils.DeriveCodeGenLanguage("test.vbproj"));
            var exception = Assert.Throws<ArgumentException>(() => GeneratorConfigurationUtils.DeriveCodeGenLanguage("test.sln"));
            Assert.Equal("Could not derive code gen language. Unrecognised project file type (.sln).", exception.Message);
        }

        [Fact]
        public void DeriveConfigFilePath()
        {
            var expectedPath = Path.Combine(_projectPath, "Nethereum.Generator.json");
            Assert.Equal(expectedPath, GeneratorConfigurationUtils.DeriveConfigFilePath(_projectPath));
        }

        [Fact]
        public void CreateNamespaceFromAssemblyName()
        {
            Assert.Equal("Company.Project", GeneratorConfigurationUtils.CreateNamespaceFromAssemblyName("Company.Project.dll"));
        }

        private string CreateSolidityFolderInParentOfProjectRoot()
        {
            var parentFolder = Directory.GetParent(_projectPath);
            var solidityFolder = Path.Combine(parentFolder.FullName, "solidity");
            if (!Directory.Exists(solidityFolder))
                Directory.CreateDirectory(solidityFolder);
            return solidityFolder;
        }
    }
}
