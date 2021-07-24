using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOVbTemplate : ClassTemplateBase<EventDTOModel>
    {
        private ParameterABIEventDTOVbTemplate _parameterAbiEventDtoVbTemplate;
        public EventDTOVbTemplate(EventDTOModel eventDTOModel) : base(eventDTOModel)
        {
            _parameterAbiEventDtoVbTemplate = new ParameterABIEventDTOVbTemplate();
            ClassFileTemplate = new VbClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}<[Event](""{Model.EventABI.Name}"")>
{SpaceUtils.OneTab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.TwoTabs}Implements IEventDTO
{SpaceUtils.TwoTabs}
{_parameterAbiEventDtoVbTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.OneTab}
{SpaceUtils.OneTab}End Class";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.TwoTabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.OneTab}End Class";

        }
    }
}