using System.Collections.Generic;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates
{
    public class FunctionABIToProtoTemplate: ClassTemplateBase<FunctionABIToProtoModel>
    {
        public FunctionABIToProtoTemplate(FunctionABIToProtoModel model):base(model)
        {
            ClassFileTemplate = new StubClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
$@"{SpaceUtils.NoTabs}
{GenerateInputMessageSchema()}
{SpaceUtils.NoTabs}
{GenerateOutputMessageSchema()}
{SpaceUtils.NoTabs}";
        }

        protected virtual IEnumerable<ParameterABI> GetInputParameters()
        {
            return Model.GetInputParameters();
        }

        protected virtual IEnumerable<ParameterABI> GetOutputParameters()
        {
            return Model.GetOutputParameters();
        }

        public string GenerateInputMessageSchema()
        {
            return ABIToProtoTemplateUtility.GenerateMessageSchema(
                Model.Name, "Request", GetInputParameters(), ParameterDirection.Input);
        }

        public string GenerateOutputMessageSchema()
        {
            if (!Model.HasOutputParameters)
                return string.Empty;

            return ABIToProtoTemplateUtility.GenerateMessageSchema(
                Model.Name, "Response", GetOutputParameters(), ParameterDirection.Output);
        }

    }
}
