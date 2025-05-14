using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOModel:TypeMessageModel
    {
        public EventABI EventABI { get; }
       
        public EventDTOModel(EventABI eventABI, string @namespace, string sharedTypesNamespace)
            :base(@namespace, eventABI.GetEventTypeNameBasedOnOverloads(), "EventDTO")
        {
            EventABI = eventABI;
            InitisialiseNamespaceDependencies(sharedTypesNamespace);
        }

        private void InitisialiseNamespaceDependencies(string sharedTypesNamespace)
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", sharedTypesNamespace });
        }

        public bool CanGenerateOutputDTO()
        {
            return true;
        }

        public bool HasParameters()
        {
            return EventABI.InputParameters != null && EventABI.InputParameters.Length > 0;
        }

    }
}