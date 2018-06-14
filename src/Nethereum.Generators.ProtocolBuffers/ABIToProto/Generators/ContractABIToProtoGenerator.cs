using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators
{
    public class ContractABIToProtoGenerator : ClassGeneratorBase<ClassTemplateBase<ContractABIToProtoModel>, ContractABIToProtoModel>
    {
        public ContractABIToProtoGenerator(ContractABI contractABI, string @namespace, string name)
        {
            var constructorAbiToProtoGenerator = new ConstructorABIToProtoGenerator(contractABI.Constructor, name);
            var functionAbiToProtoGenerators = contractABI.Functions.Select(f => new FunctionABIToProtoGenerator(f)).ToArray();
            var eventAbiToProtoGenerators = contractABI.Events.Select(e => new EventABIToProtoGenerator(e)).ToArray();
            ClassModel = new ContractABIToProtoModel(name, "", constructorAbiToProtoGenerator, functionAbiToProtoGenerators, eventAbiToProtoGenerators);
            ClassTemplate = new ContractABIToProtoTemplate(ClassModel, @namespace);
        }
        
        public GeneratedFile[] GenerateAll(string outputPath)
        {
            return new[]{
                GenerateCryptletBaseProto(outputPath),
                GenerateFileContent(outputPath)
            };
        }

        public GeneratedFile GenerateCryptletBaseProto(string outputPath)
        {
            return new GeneratedFile(cryptletBaseProtoTemplate, "cryptlet.proto", outputPath);
        }

        private string cryptletBaseProtoTemplate =
@"
syntax = ""proto3"";
package cryptlet;

option csharp_namespace = ""Cryptlet.Messages"";
option java_package = ""com.microsoft.cryptlet.messages"";
option java_multiple_files = true;

message ConstructorHeader {
}
message MessageHeader {
}
";
    }
}