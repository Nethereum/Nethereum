using System.Collections.Generic;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Models
{
    public class ConstructorABIToProtoModel : TypeMessageModel
    {
        public ConstructorABI ConstructorAbi { get; }

        public IEnumerable<ParameterABI> GetInputParameters() => ConstructorAbi.InputParameters.Ordered();

        public bool HasInputParameters
        {
            get {
                  if (ConstructorAbi.InputParameters == null) return false;
                // ReSharper disable once UseMethodAny.0
                  return ConstructorAbi.InputParameters.Count() > 0; }
        }



        public ConstructorABIToProtoModel(ConstructorABI constructorABI, string name, string classNameSuffix) : base("", name, classNameSuffix)
        {
            ConstructorAbi = constructorABI;
            CodeGenLanguage = CodeGenLanguage.Proto;
        }
    }
}