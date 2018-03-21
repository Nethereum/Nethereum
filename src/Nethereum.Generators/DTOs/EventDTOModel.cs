using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOModel:TypeMessageModel
    {
        public EventABI EventABI { get; }
        public const string SUFFIX_NAME = "EventDTO";

        public EventDTOModel(EventABI eventABI, string @namespace):base(@namespace)
        {
            EventABI = eventABI;
        }
     
        public bool CanGenerateOutputDTO()
        {
            return EventABI.InputParameters != null && EventABI.InputParameters.Length > 0;
        }

        protected override string GetClassNameSuffix()
        {
            return SUFFIX_NAME;
        }

        protected override string GetBaseName()
        {
            return EventABI.Name;
        }
    }
}