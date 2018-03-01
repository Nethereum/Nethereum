using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class EventDTOModel
    {
        private CommonGenerators commonGenerators;

        public EventDTOModel()
        {
            commonGenerators = new CommonGenerators();
        }
        public string GetEventABIOutputTypeName(EventABI eventABI)
        {
            return GetEventABIOutputTypeName(eventABI.Name);
        }

        public string GetEventABIOutputTypeName(string eventName)
        {
            return $"{commonGenerators.GenerateClassName(eventName)}EventDTO";
        }

        public bool CanGenerateOutputDTO(EventABI eventABI)
        {
            return eventABI.InputParameters != null && eventABI.InputParameters.Length > 0;
        }
    }
}