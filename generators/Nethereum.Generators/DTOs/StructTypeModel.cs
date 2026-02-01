using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeModel: TypeMessageModel
    {
        public StructABI StructTypeABI { get; }

        public StructTypeModel(StructABI structTypeABI, string @namespace, string[] referencedTypesNamespaces = null)
            : base(@namespace, structTypeABI.Name, "")
        {
            StructTypeABI = structTypeABI;
            InitialiseNamespaceDependencies(referencedTypesNamespaces);
        }

        private void InitialiseNamespaceDependencies(string[] referencedTypesNamespaces)
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes" });
            if (referencedTypesNamespaces != null && referencedTypesNamespaces.Length > 0)
            {
                NamespaceDependencies.AddRange(referencedTypesNamespaces);
            }
        }
    }
}