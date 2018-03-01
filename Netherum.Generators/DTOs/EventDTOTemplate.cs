using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class EventDTOTemplate
    {
        private ParameterABIEventDTOTemplate _parameterABIEventDTOTemplate;
        private EventDTOModel _eventDTOModel;
        public EventDTOTemplate()
        {
            _parameterABIEventDTOTemplate = new ParameterABIEventDTOTemplate();
            _eventDTOModel = new EventDTOModel();
        }

        public string GenerateFullClass(EventABI eventABI, string namespaceName)
        {
            return
                $@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace {namespaceName}
{{
{GenerateClass(eventABI)}
}}
";
        }

        public string GenerateClass(EventABI eventABI)
        {
            if (_eventDTOModel.CanGenerateOutputDTO(eventABI))
            {
                return
                    $@"{SpaceUtils.OneTab}[Event(""{eventABI.Name}"")]
{SpaceUtils.OneTab}public class {_eventDTOModel.GetEventABIOutputTypeName(eventABI)}
{SpaceUtils.OneTab}{{
{_parameterABIEventDTOTemplate.GenerateAllProperties(eventABI.InputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }
    }
}