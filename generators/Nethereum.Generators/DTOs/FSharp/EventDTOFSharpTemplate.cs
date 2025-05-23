using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOFSharpTemplate : ClassTemplateBase
    {
        public EventDTOModel Model => (EventDTOModel)ClassModel;

        private ParameterABIEventDTOFSharpTemplate _parameterAbiEventDtoFSharpTemplate;
        public EventDTOFSharpTemplate(EventDTOModel eventDTOModel) : base(eventDTOModel)
        {
            _parameterAbiEventDtoFSharpTemplate = new ParameterABIEventDTOFSharpTemplate();
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.HasParameters())
            {
                return
                    $@"{SpaceUtils.One__Tab}[<Event(""{Model.EventABI.Name}"")>]
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{SpaceUtils.Two___Tabs}inherit EventDTO()
{_parameterAbiEventDtoFSharpTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.One__Tab}";
            }
            else
            {
                return
                 $@"{SpaceUtils.One__Tab}[<Event(""{Model.EventABI.Name}"")>]
{SpaceUtils.One__Tab}type {Model.GetTypeName()}() =
{SpaceUtils.Two___Tabs}inherit EventDTO()
{SpaceUtils.One__Tab}";
            }
        }
    }

}