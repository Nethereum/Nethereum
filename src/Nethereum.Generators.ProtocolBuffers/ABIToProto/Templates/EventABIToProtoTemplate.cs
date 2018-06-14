using System.Collections.Generic;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates
{
    public class EventABIToProtoTemplate: ClassTemplateBase<EventABIToProtoModel>
    {
        public EventABIToProtoTemplate(EventABIToProtoModel model):base(model)
        {
            ClassFileTemplate = new StubClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            return
$@"{SpaceUtils.NoTabs}
{GenerateInputMessageSchema()}
{SpaceUtils.NoTabs}";
        }

        protected virtual IEnumerable<ParameterABI> GetInputParameters()
        {
            return Model.GetInputParameters();
        }

        public string GenerateInputMessageSchema()
        {
            return ABIToProtoTemplateUtility.GenerateMessageSchema(
                Model.Name, Model.ClassNameSuffix, GetInputParameters(), ParameterDirection.Input);
        }
    }
}
