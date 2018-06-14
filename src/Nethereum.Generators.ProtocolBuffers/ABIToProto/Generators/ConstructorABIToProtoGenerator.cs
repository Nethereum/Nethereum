using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators
{
    public class ConstructorABIToProtoGenerator : ClassGeneratorBase<ClassTemplateBase<ConstructorABIToProtoModel>,
        ConstructorABIToProtoModel>
    {
        public ConstructorABIToProtoGenerator(ConstructorABI constructorABI, string name)
        {
            ClassModel = new ConstructorABIToProtoModel(constructorABI, name, "CreateRequest");
            ClassTemplate = new ConstructorABIToProtoTemplate(ClassModel);
        }
    }
}