using Nethereum.Generator.Console.Configuration;
using Nethereum.Generator.Console.Models;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.Net;
using Nethereum.Generators.Tests.Common;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests.FactoryTests.FromTruffle
{
    public class FromTruffleTests
    {
        public struct TruffleFileWrapper
        {
            public TruffleFileWrapper(string jsonDirectory, string fileName)
            {
                ContractName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                Content = GetEmbeddedFileContent(fileName);
                Path = System.IO.Path.Combine(jsonDirectory, fileName);
                TruffleContract = JsonConvert.DeserializeObject<TruffleContract>(Content);
                ContractAbi =
                    new GeneratorModelABIDeserialiser().DeserialiseABI(TruffleContract.Abi.ToString());
            }

            public string ContractName { get; }
            public string Content { get; }
            public string Path { get; }
            public TruffleContract TruffleContract { get; }
            public ContractABI ContractAbi { get; }

            public static string GetEmbeddedFileContent(string resourceName)
            {
                var currentType = typeof(FromTruffleTests);
                var assembly = currentType.Assembly;
                var fullResourcePath = $"{currentType.Namespace}.Samples.{resourceName}";

                using (var stream = assembly.GetManifestResourceStream(fullResourcePath))
                {
                    if(stream == null)
                        throw new ArgumentException($"Could not find embedded resource with name '{fullResourcePath}' in assembly '{assembly.FullName}'.");

                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Should return abi configuration for each json file in the target directory
        /// </summary>
        [Fact]
        public void GeneratesConfigurationFromTruffleJsonFiles()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateEmptyProject();

                var truffleJsonDirectory = Path.Combine(context.TargetProjectFolder, "build", "contracts");

                var truffleFiles = new[]
                {
                    new TruffleFileWrapper(truffleJsonDirectory, "MetaCoin.json"), 
                    new TruffleFileWrapper(truffleJsonDirectory, "EIP20.json")
                };

                foreach (var truffleFile in truffleFiles)
                {
                    context.WriteFileToProject(truffleFile.Path, truffleFile.Content);
                }

                //when
                var config = factory.FromTruffle(
                    directory: truffleJsonDirectory,
                    outputFolder: context.TargetProjectFolder,
                    baseNamespace: "DefaultNamespace", 
                    language: CodeGenLanguage.CSharp).ToList();

                //then
                Assert.Equal(truffleFiles.Length, config.Count);

                foreach (var truffleFile in truffleFiles)
                {
                    var actualConfig = config.First(c => c.ContractName == truffleFile.ContractName);
                    Assert.NotNull(actualConfig);
                    Assert.Equal(CodeGenLanguage.CSharp, actualConfig.CodeGenLanguage);

                    Assert.Equal(
                        JsonConvert.SerializeObject(truffleFile.ContractAbi), 
                        JsonConvert.SerializeObject(actualConfig.ContractABI));

                    Assert.Equal(truffleFile.TruffleContract.Bytecode, actualConfig.ByteCode);
                    Assert.Equal(context.TargetProjectFolder, actualConfig.BaseOutputPath);

                    Assert.Equal("DefaultNamespace", actualConfig.BaseNamespace);
                    Assert.Equal($"{truffleFile.ContractName}.ContractDefinition", actualConfig.CQSNamespace);
                    Assert.Equal($"{truffleFile.ContractName}.ContractDefinition", actualConfig.DTONamespace);
                    Assert.Equal($"{truffleFile.ContractName}", actualConfig.ServiceNamespace);
                }
            }
            finally
            {
                context.CleanUp();
            }
        }


    }
}
