using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOFSharpTemplate : ClassTemplateBase<EventDTOModel>
    {
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
                    $@"{SpaceUtils.OneTab}[<Event(""{Model.EventABI.Name}"")>]
{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{SpaceUtils.TwoTabs}inherit EventDTO()
{_parameterAbiEventDtoFSharpTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.OneTab}";
            }
            else
            {
                return
                 $@"{SpaceUtils.OneTab}[<Event(""{Model.EventABI.Name}"")>]
{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{SpaceUtils.TwoTabs}inherit EventDTO()
{SpaceUtils.OneTab}";
            }
        }
    }

}