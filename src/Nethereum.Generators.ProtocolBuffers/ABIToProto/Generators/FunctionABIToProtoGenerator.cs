using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators
{
    public class FunctionABIToProtoGenerator : 
        ClassGeneratorBase<ClassTemplateBase<FunctionABIToProtoModel>, FunctionABIToProtoModel>
    {
        public FunctionABIToProtoGenerator(FunctionABI functionApi)
        {
            ClassModel = new FunctionABIToProtoModel(functionApi, functionApi.Name);
            ClassTemplate = new FunctionABIToProtoTemplate(ClassModel);
        }
    }
}
