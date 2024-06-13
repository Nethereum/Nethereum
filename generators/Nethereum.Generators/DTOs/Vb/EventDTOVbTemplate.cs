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
            if (Model.HasParameters())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}<[Event](""{Model.EventABI.Name}"")>
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Implements IEventDTO
{SpaceUtils.Two___Tabs}
{_parameterAbiEventDtoVbTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
            }
            else
            {
                return
                  $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}<[Event](""{Model.EventABI.Name}"")>
{SpaceUtils.One__Tab}Public Class {Model.GetTypeName()}Base
{SpaceUtils.Two___Tabs}Implements IEventDTO
{SpaceUtils.Two___Tabs}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}End Class";
            }
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}Public Partial Class {Model.GetTypeName()}
{SpaceUtils.Two___Tabs}Inherits {Model.GetTypeName()}Base
{SpaceUtils.One__Tab}End Class";

        }
    }
}