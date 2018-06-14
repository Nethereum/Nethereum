using System;
using System.IO;
using Nethereum.Generators.Core;
using Nethereum.Generators.ProtocolBuffers.Net.ProtoToDotNet;
using Nethereum.Generators.ProtocolBuffers.UnitTests.Common;
using Xunit;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Tests.ProtoToCSharp
{
    public class ProtoToCSharpGeneratorTests
    {
        public ProtoToCSharpGeneratorTests()
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            ProtoToDotNetConfiguration.PathToProtoC =
                $@"{userFolder}\.nuget\packages\google.protobuf.tools\3.5.1\tools\windows_x64\protoc";
        }

        [Fact]
        public void GeneratesExpectedFileName()
        {
            var generator = new ProtoToDotNetGenerator("TestProto1.proto", "TestProtoToCharp", CodeGenLanguage.CSharp);
            var actualFileName = generator.GetFileName();

            Assert.Equal("TestProtoToCharp.cs", actualFileName);
        }

        [Fact]
        public void GeneratesExpectedContent()
        {
            string protoFilePath = CreateProtoFile();

            var generator = new ProtoToDotNetGenerator(protoFilePath, "TestProtoToCharp", CodeGenLanguage.CSharp);

            var actualContent = generator.GenerateFileContent();

            Assert.NotNull(actualContent);
        }

        private static string CreateProtoFile()
        {
            var protoContent =
                EmbeddedContentRepository.Get(
                    "Nethereum.Generators.ProtocolBuffers.UnitTests.TestData.RecordHousePurchase.proto");

            var protoFilePath = Path.Combine(Path.GetTempPath(), "RecordHousePurchase.proto");

            File.WriteAllText(protoFilePath, protoContent);
            return protoFilePath;
        }
    }
}
