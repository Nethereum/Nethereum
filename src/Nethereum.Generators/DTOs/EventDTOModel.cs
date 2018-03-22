using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOModel:TypeMessageModel
    {
        public EventABI EventABI { get; }
       
        public EventDTOModel(EventABI eventABI, string @namespace)
            :base(@namespace, eventABI.Name, "EventDTO")
        {
            EventABI = eventABI;
        }
     
        public bool CanGenerateOutputDTO()
        {
            return EventABI.InputParameters != null && EventABI.InputParameters.Length > 0;
        }

    }
}