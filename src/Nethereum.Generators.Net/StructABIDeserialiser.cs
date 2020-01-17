using Nethereum.Generators.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.JsonDeserialisation;


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

        //Workaround when we don't have the internal type
        public void SetTupleTypeSameAsNameIfRequired(ContractABI contract)
        {
            foreach(var function in contract.Functions)
            {
                SetTupleTypeSameAsNameIfRequired(function.InputParameters);
                SetTupleTypeSameAsNameIfRequired(function.OutputParameters);
            }

            foreach (var eventItem in contract.Events)
            {
                SetTupleTypeSameAsNameIfRequired(eventItem.InputParameters);
            }

            if(contract.Constructor != null) {
                SetTupleTypeSameAsNameIfRequired(contract.Constructor.InputParameters);
            }
        }

        public void SetTupleTypeSameAsNameIfRequired(ParameterABI[] parameterABIs)
        {
            if (parameterABIs == null) return;
            foreach (var parameterABI in parameterABIs)
            {
                if (parameterABI.Type.StartsWith("tuple") && parameterABI.StructType == null)
                {
                    parameterABI.StructType = parameterABI.Name;
                }
            }
        }

        public string TryGetStructInternalType(IDictionary<string, object> parameter)
        {
            try
            {
                if (parameter.ContainsKey("internalType"))
                {
                    var internalType = (string)parameter["internalType"];
                    if (internalType.StartsWith("struct"))
                    {
                        var structName = internalType.Substring(internalType.LastIndexOf(".") + 1);
                        if (structName.IndexOf("[") > 0)
                        {
                            structName = structName.Substring(0, structName.IndexOf("["));
                        }

                        return structName;
                    };
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public StructABI[] GetStructsFromAbi(string abi)
        {
            var convertor = new ExpandoObjectConverter();
            var contract = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(abi, convertor);
            var structs = new List<StructABI>();

            foreach (IDictionary<string, object> element in contract)
            {
                var elementType = (string)element["type"];
                if (elementType == "function")
                    structs.AddRange(BuildStructsFromParameters((List<object>)element["outputs"]));

                if (elementType == "function" || elementType == "event" || elementType == "constructor")
                {
                    structs.AddRange(BuildStructsFromParameters((List<object>)element["inputs"]));
                }
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
            var structitem = new StructABI(GetStructTypeOrNameAsType(item));

            var parameters = new List<ParameterABI>();
            var parameterOrder = 0;
            var components = (List<object>)item["components"];
            foreach (IDictionary<string, object> component in components)
            {
                parameterOrder = parameterOrder + 1;

                if (component["type"].ToString().StartsWith("tuple"))
                {
                    var structType = GetStructTypeOrNameAsType(component);

                    var parameter = new ParameterABI((string)component["type"], (string)component["name"], parameterOrder, structType);
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

        private string GetStructTypeOrNameAsType(IDictionary<string, object> component)
        {
            var structType = TryGetStructInternalType(component);
            if (structType == null) //Workaround if we dont have the internal type
            {
                structType = (string) component["name"];
            }

            return structType;
        }
    }
}