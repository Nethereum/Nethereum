﻿using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeFSharpTemplate : ClassTemplateBase
    {
        public StructTypeModel Model => (StructTypeModel)ClassModel;

        private ParameterABIFunctionDTOFSharpTemplate _parameterAbiFunctionDtoFSharpTemplate;
        public StructTypeFSharpTemplate(StructTypeModel model) : base(model)
        {
            _parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
                return
                    $@"{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{_parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(Model.StructTypeABI.InputParameters)}
{SpaceUtils.One__Tab}";

        }
    }
}