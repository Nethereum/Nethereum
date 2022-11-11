using System.Linq;

namespace Nethereum.Generators.Model
{
    public static class FunctionEventOverloadTypeNameBuilder
    {
        public static string GetFunctionTypeNameBasedOnOverloads(this FunctionABI functionAbi)
        {
            var functionsWithSameName = functionAbi.ContractAbi.GetAllFunctionsWithSameName(functionAbi.Name);
            if (functionsWithSameName.Count == 1) return functionAbi.Name;

            var orderedList = functionsWithSameName.OrderBy(x => x.InputParameters.Length).ToArray();

            for (var i = 0; i < orderedList.Count(); i++)
            {
                if (orderedList[i] == functionAbi)
                {
                    if (i > 0) return functionAbi.Name + i.ToString();
                }    
            }
            //should never get here..
            return functionAbi.Name;
        }

        public static string GetEventTypeNameBasedOnOverloads(this EventABI eventAbi)
        {
            var eventsWithSameName = eventAbi.ContractAbi.GetAllEventsWithSameName(eventAbi.Name);
            if (eventsWithSameName.Count == 1) return eventAbi.Name;

            var orderedList = eventsWithSameName.OrderBy(x => x.InputParameters.Length).ToArray();

            for (var i = 0; i < orderedList.Count(); i++)
            {
                if (orderedList[i] == eventAbi)
                {
                    if (i > 0) return eventAbi.Name + i.ToString();
                }
            }
            //should never get here..
            return eventAbi.Name;
        }

        public static string GetErrorTypeNameBasedOnOverloads(this ErrorABI errorABI)
        {
            var errorsWithSameName = errorABI.ContractAbi.GetAllErrorsWithSameName(errorABI.Name);
            if (errorsWithSameName.Count == 1) return errorABI.Name;

            var orderedList = errorsWithSameName.OrderBy(x => x.InputParameters.Length).ToArray();

            for (var i = 0; i < orderedList.Count(); i++)
            {
                if (orderedList[i] == errorABI)
                {
                    if (i > 0) return errorABI.Name + i.ToString();
                }
            }
            //should never get here..
            return errorABI.Name;
        }
    }
}