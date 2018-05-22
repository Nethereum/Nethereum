using Nethereum.Generator.Console.Configuration;
using System.IO;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests
{
    public class GeneratorConfigurationUtilTests
    {
        private readonly string _abiFileAbsolutePath;
        private readonly string _binFileAbsolutePath;
        private readonly string _projectPath;

        public GeneratorConfigurationUtilTests()
        {
            _projectPath = Path.Combine(TestData.TempPath, "GeneratorConfigurationUtilTests");
            _abiFileAbsolutePath =
                TestData.WriteFileToFolder(_projectPath, "StandardContract.abi", TestData.StandardContract.ABI);                
            _binFileAbsolutePath =
                TestData.WriteFileToFolder(_projectPath, "StandardContract.bin", TestData.StandardContract.ByteCode);                
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
            Assert.Equal(TestData.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_BinFileCanBeAbsolutePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = _binFileAbsolutePath};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestData.StandardContract.ByteCode, abiConfiguration.ByteCode);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_BinFileCanBeARelativePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = "StandardContract.bin"};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestData.StandardContract.ByteCode, abiConfiguration.ByteCode);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_WhenBinFileIsEmptyItWillBeFoundWhenInTheSameFolderAsAbi()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABIFile = _abiFileAbsolutePath, BinFile = null, ByteCode = null};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestData.StandardContract.ByteCode, abiConfiguration.ByteCode);
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
            Assert.Equal(TestData.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_AbiFileCanBeRelativePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = Path.GetRelativePath(_projectPath, _abiFileAbsolutePath)};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestData.StandardContract.ABI, abiConfiguration.ABI);
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
            Assert.Equal(TestData.StandardContract.ABI, abiConfiguration.ABI);
        }

        [Fact]
        public void ResolveEmptyValuesWithDefaults_AbiFileCanBeAbsolutePath()
        {
            //given

            var abiConfiguration = new ABIConfiguration {ABI = null, ABIFile = _abiFileAbsolutePath};

            //when
            abiConfiguration.ResolveEmptyValuesWithDefaults("Sample.Contract", _projectPath);

            //then
            Assert.Equal(TestData.StandardContract.ABI, abiConfiguration.ABI);
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
