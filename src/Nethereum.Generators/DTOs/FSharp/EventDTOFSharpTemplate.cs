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
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{SpaceUtils.OneTab}[<Event(""{Model.EventABI.Name}"")>]
{SpaceUtils.OneTab}type {Model.GetTypeName()}() =
{SpaceUtils.TwoTabs}interface IEventDTO with
{_parameterAbiEventDtoFSharpTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.OneTab}";
            }
            return null;
        }
    }
}