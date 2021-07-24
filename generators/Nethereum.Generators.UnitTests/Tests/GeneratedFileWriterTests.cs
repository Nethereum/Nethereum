using Nethereum.Generators.Core;
using Nethereum.Generators.Net;
using Nethereum.Generators.Tests.Common.TestData;
using System.IO;
using System.Threading;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests
{
    public class GeneratedFileWriterTests
    {
        [Fact]
        public void WillNotOverwriteExistingFileIfContentIsUnChanged()
        {
            const string content = @"
using System;

namespace Tests;
public class Class1 
{
    public string Func1() 
    {
        return ""Hello"";
    }
}
";
            var generatedFile = new GeneratedFile(content, "GeneratedFileWriterTests1.cs", TestEnvironment.TempPath);

            try
            {
                var writer = new GeneratedFileWriter();

                writer.WriteFile(generatedFile);
                var creationTimestamp = new FileInfo(generatedFile.GetFullPath()).LastWriteTime.Ticks;

                Thread.Sleep(100);

                writer.WriteFile(generatedFile);
                var lastUpdateTimestamp = new FileInfo(generatedFile.GetFullPath()).LastWriteTime.Ticks;

                Assert.Equal(creationTimestamp, lastUpdateTimestamp);
            }
            finally
            {
                if(File.Exists(generatedFile.GetFullPath()))
                    File.Delete(generatedFile.GetFullPath());
            }
        }

        [Fact]
        public void WillOverwriteExistingFileWhenContentHasChanged()
        {
            var generatedFileOriginal = new GeneratedFile("Original Content", "GeneratedFileWriterTests2.cs", TestEnvironment.TempPath);
            var generatedFileChanged = new GeneratedFile("Changed Content", "GeneratedFileWriterTests2.cs", TestEnvironment.TempPath);

            try
            {
                var writer = new GeneratedFileWriter();

                writer.WriteFile(generatedFileOriginal);
                var originalTimeStamp = new FileInfo(generatedFileOriginal.GetFullPath()).LastWriteTime.Ticks;

                Thread.Sleep(100);

                writer.WriteFile(generatedFileChanged);
                var newTimestamp = new FileInfo(generatedFileChanged.GetFullPath()).LastWriteTime.Ticks;

                Assert.True(newTimestamp > originalTimeStamp);
            }
            finally
            {
                if(File.Exists(generatedFileOriginal.GetFullPath()))
                    File.Delete(generatedFileOriginal.GetFullPath());
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WillNotOutputAnEmptyFile(string content)
        {
            var generatedFile = new GeneratedFile(content, "GeneratedFileWriterTests3.cs", TestEnvironment.TempPath);

            try
            {
                var writer = new GeneratedFileWriter();
                writer.WriteFile(generatedFile);
                Assert.False(File.Exists(generatedFile.GetFullPath()));
            }
            finally
            {
                if(File.Exists(generatedFile.GetFullPath()))
                    File.Delete(generatedFile.GetFullPath());
            }
        }

    }
}
