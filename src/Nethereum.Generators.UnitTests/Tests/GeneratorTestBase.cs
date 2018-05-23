using Nethereum.Generators.Core;
using Nethereum.Generators.UnitTests.Expectations;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public abstract class GeneratorTestBase<TGenerator> where TGenerator : IFileGenerator
    {
        protected TGenerator Generator { get; }
        protected string ExpectedContentFolder { get;  }


        protected GeneratorTestBase(TGenerator generator, string expectedContentFolder)
        {
            Generator = generator;
            ExpectedContentFolder = expectedContentFolder;
        }

        public abstract void GeneratesExpectedFileContent();
        public abstract void GeneratesExpectedFileName();

        protected virtual void GenerateAndCheckFileContent(string expectedContentResourceName)
        {
            var expectedContent = ExpectedContentRepository.Get(ExpectedContentFolder, expectedContentResourceName);
            var actualFileContent = Generator.GenerateFileContent();
            Assert.Equal(expectedContent, actualFileContent);
        }

        protected virtual void GenerateAndCheckFileName(string expectedFileName)
        {
            var actualFileName = Generator.GetFileName();
            Assert.Equal(expectedFileName, actualFileName);
        }
    }
}