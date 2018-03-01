using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOGenerator : ABIServiceBase
    {
        public EventDTOTemplate template;

        public EventDTOGenerator()
        {
            template = new EventDTOTemplate();
        }

        public string GenerateFullClass(EventABI abi, string namespaceName)
        {
            return template.GenerateFullClass(abi, namespaceName);
        }

        public string GenerateFullClass(string abi, string namespaceName)
        {
            return template.GenerateFullClass(GetFirstEvent(abi), namespaceName);
        }

        public string GenerateClass(EventABI abi)
        {
            return template.GenerateClass(abi);
        }

        public string GenerateClass(string abi)
        {
            return GenerateClass(GetFirstEvent(abi));
        }
    }
}