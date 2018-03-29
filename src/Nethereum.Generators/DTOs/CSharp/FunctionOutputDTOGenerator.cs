using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOGenerator: ClassGeneratorBase<FunctionOutputDTOTemplate,FunctionOutputDTOModel>
    {
     
        public FunctionOutputDTOGenerator(FunctionABI functionABI, string @namespace)
        {
            ClassModel = new FunctionOutputDTOModel(functionABI, @namespace);
            ClassTemplate = new FunctionOutputDTOTemplate(ClassModel);
        }

        public override string GenerateClass()
        {
            return ClassModel.CanGenerateOutputDTO() ? ClassTemplate.GenerateClass() : null;
        }

        public override string GenerateFileContent()
        {
            return ClassModel.CanGenerateOutputDTO() ? ClassTemplate.GenerateFullClass() : null;
        }

    }
}