using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOGenerator: ClassGeneratorBase<EventDTOTemplate, EventDTOModel>
    { 
        public EventDTOGenerator(EventABI abi, string @namespace)
        {
            ClassModel = new EventDTOModel(abi, @namespace);
            ClassTemplate = new EventDTOTemplate(ClassModel);
        }
    }
}