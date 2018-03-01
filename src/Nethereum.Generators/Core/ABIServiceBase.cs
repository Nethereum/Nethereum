//using Nethereum.ABI.JsonDeserialisation;

using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class ABIServiceBase
    {
        protected static FunctionABI GetFirstFunction(string abi)
        {
            var contract = GetContract(abi);
            return contract.Functions[0];
        }

        protected static EventABI GetFirstEvent(string abi)
        {
            var contract = GetContract(abi);
            return contract.Events[0];
        }

        protected static ConstructorABI GetConstructorABI(string abi)
        {
            var contract = GetContract(abi);
            return contract.Constructor;
        }

        protected static ContractABI GetContract(string abi)
        {
          //  var des = new ABIDeserialiser();
           // var contract = des.DeserialiseContract(abi);
           // return contract;
            return null;
        }
    }
}