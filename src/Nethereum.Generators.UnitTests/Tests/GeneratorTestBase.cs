using Nethereum.Generators.Core;
using Nethereum.Generators.UnitTests.Expected;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public abstract class GeneratorTestBase<TGenerator> where TGenerator : IFileGenerator
    {
        protected TGenerator Generator { get; }
        protected string ExpectedContentFolder { get;  }
        protected string ExpectedContentName { get; }
        protected string ExpectedFileName { get; }

        protected GeneratorTestBase(TGenerator generator, string expectedContentFolder, string expectedContentName, string expectedFileName)
        {
            Generator = generator;
            ExpectedContentFolder = expectedContentFolder;
            ExpectedContentName = expectedContentName;
            ExpectedFileName = expectedFileName;
        }

        [Fact]
        public void GeneratesExpectedFileContent()
        {
            var expectedContent = ExpectedContentRepository.Get(ExpectedContentFolder, ExpectedContentName);
            var actualFileContent = Generator.GenerateFileContent();
            Assert.Equal(expectedContent, actualFileContent);
        }

        [Fact]
        public void GeneratesExpectedFileName()
        {
            var actualFileName = Generator.GetFileName();
            Assert.Equal(ExpectedFileName, actualFileName);
        }
    }
}