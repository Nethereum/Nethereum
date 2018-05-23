using Nethereum.Generator.Console.Configuration;
using System.IO;
using Nethereum.Generators.Tests.Common.TestData;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;
using Nethereum.Generators.Core;
using System;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests
{
    public class GeneratorConfigurationUtilTests
    {
        private readonly string _abiFileAbsolutePath;
        private readonly string _binFileAbsolutePath;
        private readonly string _projectPath;

        public GeneratorConfigurationUtilTests()
        {
            _projectPath = Path.Combine(TestEnvironment.TempPath, "GeneratorConfigurationUtilTests");
            _abiFileAbsolutePath =
                TestEnvironment.WriteFileToFolder(_projectPath, "StandardContract.abi", TestContracts.StandardContract.ABI);                
            _binFileAbsolutePath =
                TestEnvironment.WriteFileToFolder(_projectPath, "StandardContract.bin", TestContracts.StandardContract.ByteCode);                
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_EmptyContractNameIsDefaultedToNameOfAbiFile()
        {
            //given
            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, ContractName = null};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal("StandardContract", abiConfiguration.ContractName);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_WhenAbiContentIsEmptyItIsReadFromAbiFile()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = _abiFileAbsolutePath};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_BinFileCanBeAbsolutePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = _binFileAbsolutePath};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfiguration.ByteCode);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_BinFileCanBeARelativePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = "StandardContract.bin"};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfiguration.ByteCode);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_WhenBinFileIsEmptyItWillBeFoundWhenInTheSameFolderAsAbi()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = null, ByteCode = null};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfiguration.ByteCode);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_BinFileCanBeInParentFolderOfTheProjectRoot()
        {
            //given
            string solidityFolder = CreateSolidityFolderInParentOfProjectRoot();
            var abiInSolidityFolder = Path.Combine(solidityFolder, Path.GetFileName(_abiFileAbsolutePath));
            var binInSolidityFolder = Path.Combine(solidityFolder, Path.GetFileName(_binFileAbsolutePath));
            File.Copy(_abiFileAbsolutePath, abiInSolidityFolder, true);
            File.Copy(_abiFileAbsolutePath, binInSolidityFolder, true);

            var abiConfiguration = new ABIConfiguration { ABI = null, ABIFile = "..\\solidity\\StandardContract.abi", BinFile = "..\\solidity\\StandardContract.bin" };

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_AbiFileCanBeRelativePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = Path.GetRelativePath(_projectPath, _abiFileAbsolutePath)};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_AbiFileCanBeInParentFolderOfTheProjectRoot()
        {
            //given
            var solidityFolder = CreateSolidityFolderInParentOfProjectRoot();
            var abiInSolidityFolder = Path.Combine(solidityFolder, Path.GetFileName(_abiFileAbsolutePath));
            File.Copy(_abiFileAbsolutePath, abiInSolidityFolder, true);

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = "..\\solidity\\StandardContract.abi"};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_AbiFileCanBeAbsolutePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = _abiFileAbsolutePath};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestContracts.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void GetFullFileAndFolderPaths_GivenFileFullPath_ReturnsBothFullPaths()
        {
            (string folder, string file) = GeneratorConfigurationUtils.GetFullFileAndFolderPaths(_abiFileAbsolutePath);
            Assert.Equal(_projectPath, folder);
            Assert.Equal(_abiFileAbsolutePath, file);
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
