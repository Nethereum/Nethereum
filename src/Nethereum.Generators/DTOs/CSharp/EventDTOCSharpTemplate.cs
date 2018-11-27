using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOCSharpTemplate: ClassTemplateBase<EventDTOModel>
    {
        private ParameterABIEventDTOCSharpTemplate _parameterAbiEventDtocSharpTemplate;
        public EventDTOCSharpTemplate(EventDTOModel eventDTOModel):base(eventDTOModel)
        {
            _parameterAbiEventDtocSharpTemplate = new ParameterABIEventDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.CanGenerateOutputDTO())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.OneTab}[Event(""{Model.EventABI.Name}"")]
{SpaceUtils.OneTab}public class {Model.GetTypeName()}Base : IEventDTO
{SpaceUtils.OneTab}{{
{_parameterAbiEventDtocSharpTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}