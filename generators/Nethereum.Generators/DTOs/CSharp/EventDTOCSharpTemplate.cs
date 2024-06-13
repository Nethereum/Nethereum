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
            if (Model.HasParameters())
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}[Event(""{Model.EventABI.Name}"")]
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : IEventDTO
{SpaceUtils.One__Tab}{{
{_parameterAbiEventDtocSharpTemplate.GenerateAllProperties(Model.EventABI.InputParameters)}
{SpaceUtils.One__Tab}}}";
            }
            else
            {
                return
                    $@"{GetPartialMainClass()}

{SpaceUtils.One__Tab}[Event(""{Model.EventABI.Name}"")]
{SpaceUtils.One__Tab}public class {Model.GetTypeName()}Base : IEventDTO
{SpaceUtils.One__Tab}{{
{SpaceUtils.One__Tab}}}";
            }
          
        }

        public string GetPartialMainClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : {Model.GetTypeName()}Base {{ }}";
        }
    }
}