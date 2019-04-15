using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Generators.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Generators.Net
{
    public class StructABIDeserialiser
    {
        class StructABIEqualityComparer : IEqualityComparer<StructABI>
        {
            public bool Equals(StructABI x, StructABI y)
            {
                return string.Equals(x.Name, y.Name);
            }

            public int GetHashCode(StructABI obj)
            {
                if (obj.Name == null) return 0;
                return obj.Name.GetHashCode();
            }
        }

        public void SetTupleTypeSameAsName(ContractABI contract)
        {
            foreach(var function in contract.Functions)
            {
                SetTupleTypeSameAsName(function.InputParameters);
                SetTupleTypeSameAsName(function.OutputParameters);
            }

            foreach (var eventItem in contract.Events)
            {
                SetTupleTypeSameAsName(eventItem.InputParameters);
            }

            if(contract.Constructor != null) { 
                SetTupleTypeSameAsName(contract.Constructor.InputParameters);
            }
        }

        public void SetTupleTypeSameAsName(ParameterABI[] parameterABIs)
        {
            if (parameterABIs == null) return;
            foreach (var parameterABI in parameterABIs)
            {
                if (parameterABI.Type.StartsWith("tuple"))
                {
                    parameterABI.StructType = parameterABI.Name;
                }
            }
        }

        public StructABI[] GetStructsFromAbi(string abi)
        {
            var convertor = new ExpandoObjectConverter();
            var contract = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(abi, convertor);
            var structs = new List<StructABI>();

            foreach (IDictionary<string, object> element in contract)
            {
                if ((string)element["type"] == "function")
                    structs.AddRange(BuildStructsFromParameters((List<object>)element["outputs"]));
                structs.AddRange(BuildStructsFromParameters((List<object>)element["inputs"]));
            }
            return GetDistinctStructsByTypeName(structs);
        }

        public StructABI[] GetDistinctStructsByTypeName(List<StructABI> structs)
        {
            return structs.Distinct(new StructABIEqualityComparer()).ToArray();
        }

        public StructABI[] BuildStructsFromParameters(List<object> items)
        {
            var structs = new List<StructABI>();
            foreach(IDictionary<string, object> item in items) { 
            
                if (item["type"].ToString().StartsWith("tuple")) {
                   structs.AddRange(BuildStructsFromTuple(item));
                }
            }
            return structs.ToArray();
        }

        public StructABI[] BuildStructsFromTuple(IDictionary<string, object> item)
        {
            var structs = new List<StructABI>();
            var structitem = new StructABI((string)item["name"]);

            var parameters = new List<ParameterABI>();
            var parameterOrder = 0;
            var components = (List<object>)item["components"];
            foreach (IDictionary<string, object> component in components)
            {
                parameterOrder = parameterOrder + 1;

                if (component["type"].ToString().StartsWith("tuple")) {
                    var parameter = new ParameterABI((string)component["type"], (string)component["name"], parameterOrder, (string)component["name"]);
                    structs.AddRange(BuildStructsFromTuple(component));
                    parameters.Add(parameter);
                }
                else
                {
                    var parameter = new ParameterABI((string)component["type"], (string)component["name"], parameterOrder);
                    parameters.Add(parameter);
                }
            }

            structitem.InputParameters = parameters.ToArray();
            structs.Add(structitem);
            return structs.ToArray();
        }

    }
}