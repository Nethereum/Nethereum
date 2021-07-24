using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeModel: TypeMessageModel
    {
        public StructABI StructTypeABI { get; }

        public StructTypeModel(StructABI structTypeABI, string @namespace)
            : base(@namespace, structTypeABI.Name, "")
        {
            StructTypeABI = structTypeABI;
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] { "System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes" });
        }
    }
}