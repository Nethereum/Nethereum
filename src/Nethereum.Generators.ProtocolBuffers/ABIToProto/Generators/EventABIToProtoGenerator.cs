using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators
{
    public class EventABIToProtoGenerator :
        ClassGeneratorBase<ClassTemplateBase<EventABIToProtoModel>, EventABIToProtoModel>
    {
        public EventABIToProtoGenerator(EventABI eventABI)
        {
            ClassModel = new EventABIToProtoModel(eventABI, eventABI.Name);
            ClassTemplate = new EventABIToProtoTemplate(ClassModel);
        }
    }
}
