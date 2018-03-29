using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOTemplate: IClassTemplate
    {
        private ParameterABIEventDTOTemplate _parameterABIEventDTOTemplate;
        private EventDTOModel _eventDTOModel;
        public EventDTOTemplate(EventDTOModel eventDTOModel)
        {
            _parameterABIEventDTOTemplate = new ParameterABIEventDTOTemplate();
            _eventDTOModel = eventDTOModel;
        }

        public string GenerateFullClass()
        {
            return
$@"{SpaceUtils.NoTabs}using System;
{SpaceUtils.NoTabs}using System.Threading.Tasks;
{SpaceUtils.NoTabs}using System.Numerics;
{SpaceUtils.NoTabs}using Nethereum.Hex.HexTypes;
{SpaceUtils.NoTabs}using Nethereum.ABI.FunctionEncoding.Attributes;
{SpaceUtils.NoTabs}namespace {_eventDTOModel.Namespace}
{SpaceUtils.NoTabs}{{
{SpaceUtils.NoTabs}{GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }

        public string GenerateClass()
        {
            if (_eventDTOModel.CanGenerateOutputDTO())
            {
                return
                    $@"{SpaceUtils.OneTab}[Event(""{_eventDTOModel.EventABI.Name}"")]
{SpaceUtils.OneTab}public class {_eventDTOModel.GetTypeName()}
{SpaceUtils.OneTab}{{
{_parameterABIEventDTOTemplate.GenerateAllProperties(_eventDTOModel.EventABI.InputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }
    }
}